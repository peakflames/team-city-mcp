namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>Thrown by every internal query path for a definitive "we could not get an answer"
/// failure. Never thrown for a real <c>count:0</c> — that is a normal <see cref="GateDecision.Deny"/>,
/// not an error. Caught exactly once, at each public <see cref="IPermissionGate"/> member, and
/// converted to a <see cref="GateDecision.Deny"/> carrying <see cref="Reason"/> — every reason in
/// this taxonomy renders as the byte-identical <c>ToolGate.DeniedMessage</c> to the caller.
/// Thrown from inside a <see cref="Caching.TtlCache{TKey,TValue}"/> factory, so the faulted lookup is
/// never cached.</summary>
internal sealed class PermissionQueryException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

/// <summary>
/// Replaces <c>AlwaysAllowPermissionGate</c> — queries TeamCity's own already-computed permission
/// decision via <c>GET /app/rest/users/{locator}/permissions</c>, backed by a
/// <see cref="Caching.TtlCache{TKey,TValue}"/> keyed on <see cref="Caching.PermissionCacheKey"/>.
///
/// Locator syntax is bare <c>permission:{perm}</c> (never <c>permission:(id:{perm})</c> — that is
/// HTTP 400) and <c>project:(id:{projectId})</c> for a single-project check, or <c>global:true</c>
/// for a global check — never both. <c>count &gt;= 1</c>, never <c>== 1</c> (a user can hold the same
/// permission from more than one role assignment). Every non-answer — bad request, forbidden, not
/// found, 5xx, timeout, transport error, malformed body — throws <see cref="PermissionQueryException"/>
/// from inside the cache factory, so nothing but a real answer is ever cached.
///
/// Takes <see cref="IServiceProvider"/>, not <see cref="ITeamCityClientFactory"/> directly: this gate
/// is a singleton (so its cache survives across calls), and a singleton holding a transient
/// <see cref="ITeamCityClientFactory"/> would pin one <c>HttpClient</c> handler chain for the life of
/// the process (stale DNS, no socket recycling). Every upstream call instead opens its own
/// <c>CreateAsyncScope()</c> and resolves the factory fresh — the same shape every tool body and
/// <c>ToolGate.BeginCoreAsync</c> already use.
/// </summary>
public sealed class TeamCityPermissionGate : IPermissionGate
{
    // Derived from probe 10: N=100 ids -> 3,961-char locator -> 200 OK; N=150 -> 7,888 chars ->
    // Tomcat's own HTML 400 (the container rejects the request line before TeamCity ever parses it).
    // The budget is a URL-length limit, not a fixed count, so it is enforced by character count with
    // a secondary hard cap at 100 ids as a backstop.
    internal const int BatchCharBudget = 3800;
    internal const int BatchMaxCount = 100;

    private static readonly Regex NumericIdentity = new("^[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex SafeProjectId = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

    private readonly IServiceProvider _serviceProvider;
    private readonly Caching.TtlCache<Caching.PermissionCacheKey, bool> _permissionCache;
    private readonly Caching.TtlCache<Caching.VisibleSetCacheKey, VisibleProjectSet> _visibleSetCache;
    private readonly TimeSpan _permissionTtl;
    private readonly TimeSpan _visibleSetTtl;
    private readonly ILogger<TeamCityPermissionGate> _logger;

    private int _consecutiveFailures;

    public TeamCityPermissionGate(
        IServiceProvider serviceProvider,
        IOptions<RbacOptions> options,
        TimeProvider timeProvider,
        ILogger<TeamCityPermissionGate> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _permissionTtl = TimeSpan.FromSeconds(options.Value.PermissionCacheTtlSeconds);
        _permissionCache = new Caching.TtlCache<Caching.PermissionCacheKey, bool>(
            options.Value.MaxCacheEntries, timeProvider, logger, "Rbac:PermissionCache");
        _visibleSetTtl = TimeSpan.FromSeconds(options.Value.VisibleSetCacheTtlSeconds);
        _visibleSetCache = new Caching.TtlCache<Caching.VisibleSetCacheKey, VisibleProjectSet>(
            options.Value.MaxVisibleSetCacheEntries, timeProvider, logger, "Rbac:VisibleSetCache");
    }

    public bool Enabled => true;

    /// <summary>Exposed so <c>AddRbac</c> can register this instance's cache under
    /// <see cref="Caching.IEvictableCache"/> for <see cref="Caching.RbacCacheJanitor"/>.</summary>
    internal Caching.IEvictableCache Cache => _permissionCache;

    /// <summary>Exposed for the same reason as <see cref="Cache"/> — a distinct cache instance
    /// (visible-set entries are thousands of strings each; a permission entry is one <c>bool</c>),
    /// so it gets its own capacity knob, <c>Rbac:MaxVisibleSetCacheEntries</c>, rather than sharing
    /// <c>Rbac:MaxCacheEntries</c>.</summary>
    internal Caching.IEvictableCache VisibleSetCache => _visibleSetCache;

    public async ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default)
    {
        if (!ToolResourcePermissionMap.TryGet(toolName, out var spec))
        {
            _logger.LogError("RBAC permission gate got a check for unmapped tool '{ToolName}'.", toolName);
            return GateDecision.Deny("tool_not_mapped");
        }

        if (spec.Permission is null)
        {
            _logger.LogError("RBAC permission gate got a check for tool '{ToolName}' with no mapped permission.", toolName);
            return GateDecision.Deny("no_permission_mapped");
        }

        var key = new Caching.PermissionCacheKey(identity, spec.Permission, projectId);

        try
        {
            var granted = await _permissionCache.GetOrAddAsync(
                key,
                _permissionTtl,
                ct => QuerySingleAsync(identity, spec.Permission, projectId, ct),
                cancellationToken);

            return granted ? GateDecision.Allow() : GateDecision.Deny("count_zero");
        }
        catch (PermissionQueryException ex)
        {
            return GateDecision.Deny(ex.Reason);
        }
    }

    public async ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default)
    {
        if (!ToolResourcePermissionMap.TryGet(toolName, out var spec))
        {
            _logger.LogError("RBAC permission gate got a check for unmapped tool '{ToolName}'.", toolName);
            return GateDecision.Deny("tool_not_mapped");
        }

        if (spec.Permission is null)
        {
            _logger.LogError("RBAC permission gate got a check for tool '{ToolName}' with no mapped permission.", toolName);
            return GateDecision.Deny("no_permission_mapped");
        }

        // Deliberately a distinct cache key (ProjectId: null) from any project-scoped check, and
        // deliberately never falls back to an unscoped query: an unscoped permission:X query returns
        // count>=1 if the identity holds X on *any single project*, which would let one project
        // grant authorize a server-wide permission.
        var key = new Caching.PermissionCacheKey(identity, spec.Permission, ProjectId: null);

        try
        {
            var granted = await _permissionCache.GetOrAddAsync(
                key,
                _permissionTtl,
                ct => QuerySingleAsync(identity, spec.Permission, projectId: null, ct),
                cancellationToken);

            return granted ? GateDecision.Allow() : GateDecision.Deny("count_zero");
        }
        catch (PermissionQueryException ex)
        {
            return GateDecision.Deny(ex.Reason);
        }
    }

    public async ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
            return GateDecision.Allow();

        if (!ToolResourcePermissionMap.TryGet(toolName, out var spec))
            return GateDecision.Deny("tool_not_mapped");

        if (spec.Permission is null)
            return GateDecision.Deny("no_permission_mapped");

        var permission = spec.CrossProjectPermission ?? spec.Permission;
        var distinctIds = projectIds.Distinct(StringComparer.Ordinal).ToArray();

        try
        {
            foreach (var chunk in ChunkProjectIds(distinctIds))
            {
                var granted = await QueryBatchAsync(identity, permission, chunk, cancellationToken);

                foreach (var projectId in chunk)
                {
                    _permissionCache.Set(
                        new Caching.PermissionCacheKey(identity, permission, projectId), granted.Contains(projectId), _permissionTtl);
                }

                // A partial visible set is a wrong answer, not a degraded one — any chunk with a
                // missing grant denies the whole call, and any chunk failure (below) does too.
                if (chunk.Any(id => !granted.Contains(id)))
                    return GateDecision.Deny("count_zero");
            }

            return GateDecision.Allow();
        }
        catch (PermissionQueryException ex)
        {
            return GateDecision.Deny(ex.Reason);
        }
    }

    /// <summary>Cached (<c>Rbac:VisibleSetCacheTtlSeconds</c>) by <c>(identity, permission)</c> — a
    /// G4 tool's visible-set query is a ~500 KB response in the worst case, unlike the single-bit
    /// answer <see cref="CheckProjectAsync"/> caches. A project-less entry in the response (a global
    /// grant) becomes <see cref="VisibleProjectSet.IsGlobal"/>, never a finite list — a server admin
    /// must never have "every project that exists today" materialized and frozen at query time.
    /// </summary>
    public async ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
        string toolName, string identity, CancellationToken cancellationToken = default)
    {
        if (!ToolResourcePermissionMap.TryGet(toolName, out var spec) || spec.Permission is null)
            return VisibleProjectSet.Scoped(new HashSet<string>(StringComparer.Ordinal));

        var key = new Caching.VisibleSetCacheKey(identity, spec.Permission);

        try
        {
            return await _visibleSetCache.GetOrAddAsync(
                key,
                _visibleSetTtl,
                ct => QueryVisibleSetAsync(identity, spec.Permission, ct),
                cancellationToken);
        }
        catch (PermissionQueryException)
        {
            // Fail closed: an inability to compute the visible set is never "visible everywhere".
            return VisibleProjectSet.Scoped(new HashSet<string>(StringComparer.Ordinal));
        }
    }

    /// <summary>G5 fan-out pruning: returns exactly the granted subset of <paramref name="projectIds"/>,
    /// reusing the same batched <see cref="QueryBatchAsync"/>/<see cref="ChunkProjectIds"/> machinery
    /// as <see cref="CheckProjectsAsync"/>, but never denies the whole call over a partial grant — the
    /// caller prunes the ungranted ids instead of being denied outright. Also warms the per-project
    /// permission cache exactly like <see cref="CheckProjectsAsync"/> does, so a later single-project
    /// check on one of these same ids is a cache hit.</summary>
    public async ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        if (!ToolResourcePermissionMap.TryGet(toolName, out var spec) || spec.Permission is null)
            return new HashSet<string>(StringComparer.Ordinal);

        var permission = spec.CrossProjectPermission ?? spec.Permission;
        var distinctIds = projectIds.Distinct(StringComparer.Ordinal).ToArray();
        var granted = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (var chunk in ChunkProjectIds(distinctIds, permission))
            {
                var chunkGranted = await QueryBatchAsync(identity, permission, chunk, cancellationToken);

                foreach (var projectId in chunk)
                {
                    _permissionCache.Set(
                        new Caching.PermissionCacheKey(identity, permission, projectId), chunkGranted.Contains(projectId), _permissionTtl);

                    if (chunkGranted.Contains(projectId))
                        granted.Add(projectId);
                }
            }
        }
        catch (PermissionQueryException)
        {
            // Fail closed: an inability to compute the filtered set is never "visible everywhere" —
            // an upstream failure mid-fan-out prunes everything not yet confirmed, same direction as
            // GetVisibleProjectSetAsync's own catch below.
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return granted;
    }

    private async Task<VisibleProjectSet> QueryVisibleSetAsync(string identity, string permission, CancellationToken ct)
    {
        ValidateIdentity(identity);

        var locator = $"permission:{permission}";
        var url = BuildPermissionsUrl(identity, locator, multiProject: true);

        using var response = await GetPermissionsAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        var parsed = DeserializeAssignments(json);

        var projectIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in parsed.PermissionAssignment ?? [])
        {
            if (entry.Project?.Id is { } id)
            {
                projectIds.Add(id);
            }
            else
            {
                // A project-less entry is a global grant — visible everywhere, not just the
                // projects named so far in this response.
                return VisibleProjectSet.Global();
            }
        }

        return VisibleProjectSet.Scoped(projectIds);
    }

    // ---- Locator construction (internal + static: unit-tested by PermissionLocatorTests without
    // any HTTP or DI). ----

    internal static string BuildSingleProjectLocator(string permission, string projectId) =>
        $"permission:{permission},project:(id:{projectId})";

    internal static string BuildGlobalLocator(string permission) =>
        $"permission:{permission},global:true";

    internal static string BuildBatchLocator(string permission, IReadOnlyCollection<string> projectIds) =>
        $"permission:{permission},project:(id:({string.Join(",", projectIds)}))";

    internal static IEnumerable<IReadOnlyList<string>> ChunkProjectIds(IReadOnlyList<string> projectIds, string permission = "view_project")
    {
        var current = new List<string>();

        foreach (var projectId in projectIds)
        {
            var candidate = new List<string>(current) { projectId };
            var wouldBeLocatorLength = BuildBatchLocator(permission, candidate).Length;

            if (current.Count > 0 && (candidate.Count > BatchMaxCount || wouldBeLocatorLength > BatchCharBudget))
            {
                yield return current;
                current = [projectId];
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Count > 0)
            yield return current;
    }

    // ---- Query execution ----

    private async Task<bool> QuerySingleAsync(string identity, string permission, string? projectId, CancellationToken ct)
    {
        ValidateIdentity(identity);
        if (projectId is not null)
            ValidateProjectId(projectId);

        var locator = projectId is null ? BuildGlobalLocator(permission) : BuildSingleProjectLocator(permission, projectId);
        var url = BuildPermissionsUrl(identity, locator, multiProject: false);

        using var response = await GetPermissionsAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        RbacPermissionCountResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(json, RbacJsonContext.Default.RbacPermissionCountResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "RBAC permission gate got a malformed response body.");
            throw new PermissionQueryException("upstream_malformed_response");
        }

        if (parsed?.Count is null)
        {
            _logger.LogError("RBAC permission gate got a response with no 'count' field.");
            throw new PermissionQueryException("upstream_malformed_response");
        }

        return parsed.Count.Value >= 1;
    }

    private async Task<HashSet<string>> QueryBatchAsync(
        string identity, string permission, IReadOnlyList<string> projectIds, CancellationToken ct)
    {
        ValidateIdentity(identity);
        foreach (var projectId in projectIds)
            ValidateProjectId(projectId);

        var locator = BuildBatchLocator(permission, projectIds);
        var url = BuildPermissionsUrl(identity, locator, multiProject: true);

        using var response = await GetPermissionsAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        var parsed = DeserializeAssignments(json);

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in parsed.PermissionAssignment ?? [])
        {
            if (entry.Project?.Id is { } id)
                granted.Add(id);
        }

        return granted;
    }

    private RbacPermissionAssignmentResponse DeserializeAssignments(string json)
    {
        RbacPermissionAssignmentResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(json, RbacJsonContext.Default.RbacPermissionAssignmentResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "RBAC permission gate got a malformed response body.");
            throw new PermissionQueryException("upstream_malformed_response");
        }

        if (parsed?.Count is null)
        {
            _logger.LogError("RBAC permission gate got a response with no 'count' field.");
            throw new PermissionQueryException("upstream_malformed_response");
        }

        return parsed;
    }

    internal static string BuildPermissionsUrl(string identity, string locator, bool multiProject)
    {
        var fields = multiProject ? "count,permissionAssignment(project(id))" : "count";
        return $"app/rest/users/id:{identity}/permissions?locator={Uri.EscapeDataString(locator)}&fields={fields}";
    }

    private static void ValidateIdentity(string identity)
    {
        if (!NumericIdentity.IsMatch(identity))
            throw new PermissionQueryException("invalid_identity");
    }

    private static void ValidateProjectId(string projectId)
    {
        if (!SafeProjectId.IsMatch(projectId))
            throw new PermissionQueryException("invalid_project_id");
    }

    private async Task<HttpResponseMessage> GetPermissionsAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();

        if (clientResult.IsFailed)
        {
            RecordFailure();
            _logger.LogError(
                "RBAC permission gate could not create a TeamCity client: {Message}", clientResult.Errors.First().Message);
            throw new PermissionQueryException("client_unavailable");
        }

        HttpResponseMessage response;
        try
        {
            response = await clientResult.Value.HttpClient.GetAsync(relativeUrl, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("RBAC permission gate query was canceled by the caller.");
            throw new PermissionQueryException("request_canceled");
        }
        catch (OperationCanceledException ex)
        {
            RecordFailure();
            _logger.LogError(ex, "RBAC permission gate query timed out.");
            throw new PermissionQueryException("upstream_timeout");
        }
        catch (HttpRequestException ex)
        {
            RecordFailure();
            _logger.LogError(ex, "RBAC permission gate query hit a transport error.");
            throw new PermissionQueryException("upstream_transport_error");
        }

        if (!response.IsSuccessStatusCode)
        {
            var reason = ReasonForStatus(response.StatusCode);
            if (reason == "upstream_unexpected_status")
                _logger.LogError("RBAC permission gate got unexpected HTTP {StatusCode}.", (int)response.StatusCode);
            else if (response.StatusCode == HttpStatusCode.NotFound)
                _logger.LogWarning("RBAC permission gate got HTTP 404 (user not found) for identity lookup.");
            else
                _logger.LogError("RBAC permission gate got HTTP {StatusCode} ({Reason}).", (int)response.StatusCode, reason);

            if (response.StatusCode != HttpStatusCode.NotFound)
                RecordFailure();

            response.Dispose();
            throw new PermissionQueryException(reason);
        }

        RecordSuccess();
        return response;
    }

    private static string ReasonForStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "upstream_bad_request",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "upstream_forbidden",
        HttpStatusCode.NotFound => "user_not_found",
        >= HttpStatusCode.InternalServerError => "upstream_unavailable",
        _ => "upstream_unexpected_status",
    };

    private void RecordFailure()
    {
        var count = Interlocked.Increment(ref _consecutiveFailures);
        if (count == 1 || count % 50 == 0)
        {
            _logger.LogError("RBAC permission gate has now failed {ConsecutiveFailures} consecutive upstream calls.", count);
        }
    }

    private void RecordSuccess()
    {
        var previous = Interlocked.Exchange(ref _consecutiveFailures, 0);
        if (previous > 0)
        {
            _logger.LogInformation("RBAC permission gate recovered after {FailureCount} consecutive failures.", previous);
        }
    }
}
