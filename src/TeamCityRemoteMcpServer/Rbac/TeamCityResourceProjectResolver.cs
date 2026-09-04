namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// Mirrors <see cref="TeamCityPermissionGate"/>'s shape exactly: a singleton taking
/// <see cref="IServiceProvider"/> (never <see cref="ITeamCityClientFactory"/> directly — a singleton
/// holding a transient factory would pin one <c>HttpClient</c> handler chain for the life of the
/// process), a per-call <c>CreateAsyncScope()</c>, and a <see cref="ResourcePivotException"/>-shaped
/// failure taxonomy so every failure converts to the same <c>ToolGate.DeniedMessage</c>.
///
/// Two caches, not one: buildType->project is NOT "effectively immutable" — live testing found build
/// configs do move between projects, hence the shorter <see cref="RbacOptions.BuildTypeProjectCacheTtlSeconds"/>.
/// build->project IS immutable once a build exists (a build never moves to a different build type),
/// hence the longer <see cref="RbacOptions.BuildProjectCacheTtlSeconds"/>. vcsRoot->project has no
/// cache — <c>teamcity_get_vcs_root</c> is the only tool that needs it, so a cache would add
/// complexity without meaningfully reducing upstream calls.
/// </summary>
public sealed class TeamCityResourceProjectResolver : IResourceProjectResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Caching.TtlCache<string, string> _buildTypeProjectCache;
    private readonly Caching.TtlCache<string, string> _buildProjectCache;
    private readonly TimeSpan _buildTypeProjectTtl;
    private readonly TimeSpan _buildProjectTtl;
    private readonly ILogger<TeamCityResourceProjectResolver> _logger;

    public TeamCityResourceProjectResolver(
        IServiceProvider serviceProvider,
        IOptions<RbacOptions> options,
        TimeProvider timeProvider,
        ILogger<TeamCityResourceProjectResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _buildTypeProjectTtl = TimeSpan.FromSeconds(options.Value.BuildTypeProjectCacheTtlSeconds);
        _buildProjectTtl = TimeSpan.FromSeconds(options.Value.BuildProjectCacheTtlSeconds);
        _buildTypeProjectCache = new Caching.TtlCache<string, string>(
            options.Value.MaxCacheEntries, timeProvider, logger, "Rbac:BuildTypeProjectCache");
        _buildProjectCache = new Caching.TtlCache<string, string>(
            options.Value.MaxCacheEntries, timeProvider, logger, "Rbac:BuildProjectCache");
    }

    /// <summary>Exposed so <c>AddRbac</c> can register both caches under
    /// <see cref="Caching.IEvictableCache"/> for <see cref="Caching.RbacCacheJanitor"/>.</summary>
    internal Caching.IEvictableCache BuildTypeProjectCache => _buildTypeProjectCache;

    internal Caching.IEvictableCache BuildProjectCache => _buildProjectCache;

    public async ValueTask<string> ResolveProjectForBuildTypeAsync(string buildTypeId, CancellationToken cancellationToken = default)
    {
        return await _buildTypeProjectCache.GetOrAddAsync(
            buildTypeId, _buildTypeProjectTtl, ct => QueryBuildTypeProjectAsync(buildTypeId, ct), cancellationToken);
    }

    public async ValueTask<string> ResolveProjectForBuildAsync(string buildId, CancellationToken cancellationToken = default)
    {
        return await _buildProjectCache.GetOrAddAsync(
            buildId, _buildProjectTtl, ct => QueryBuildProjectAsync(buildId, ct), cancellationToken);
    }

    public async ValueTask<string> ResolveProjectForVcsRootAsync(string vcsRootId, CancellationToken cancellationToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var client = await CreateClientOrThrowAsync(scope, cancellationToken);

        var pivot = await TeamCityPivotQueries.ResolveVcsRootProjectAsync(client, vcsRootId, cancellationToken);
        return ToProjectIdOrThrow(pivot.Outcome, pivot.ProjectId, "pivot_vcsroot_unresolved");
    }

    private async Task<string> QueryBuildTypeProjectAsync(string buildTypeId, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var client = await CreateClientOrThrowAsync(scope, cancellationToken);

        var pivot = await TeamCityPivotQueries.ResolveBuildTypeProjectAsync(client, buildTypeId, cancellationToken);
        return ToProjectIdOrThrow(pivot.Outcome, pivot.ProjectId, "pivot_buildtype_unresolved");
    }

    private async Task<string> QueryBuildProjectAsync(string buildId, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var client = await CreateClientOrThrowAsync(scope, cancellationToken);

        var pivot = await TeamCityPivotQueries.ResolveBuildAsync(client, buildId, cancellationToken);
        return ToProjectIdOrThrow(pivot.Outcome, pivot.ProjectId, "pivot_build_unresolved");
    }

    private async Task<TeamCityClient> CreateClientOrThrowAsync(AsyncServiceScope scope, CancellationToken cancellationToken)
    {
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();

        if (clientResult.IsFailed)
        {
            _logger.LogError(
                "RBAC resource resolver could not create a TeamCity client: {Message}", clientResult.Errors.First().Message);
            throw new ResourcePivotException("pivot_client_unavailable");
        }

        return clientResult.Value;
    }

    private static string ToProjectIdOrThrow(TeamCityPivotQueries.PivotOutcome outcome, string? projectId, string notFoundReason)
    {
        if (outcome == TeamCityPivotQueries.PivotOutcome.UpstreamError)
            throw new ResourcePivotException("pivot_upstream_error");

        if (outcome != TeamCityPivotQueries.PivotOutcome.Found || projectId is not { Length: > 0 })
            throw new ResourcePivotException(notFoundReason);

        return projectId;
    }
}
