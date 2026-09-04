namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// A routable fake for the outbound <c>HttpClient</c> the TeamCity client uses — installed via
/// <see cref="TeamCityFakeFactory"/>'s <c>ConfigureHttpClientDefaults</c>, not by naming the typed
/// client (the 2-generic <c>AddHttpClient&lt;TClient,TImpl&gt;</c> name derives from
/// <c>TClient</c>, a known footgun). Routes are matched in registration order, first match wins.
/// <see cref="RequestLog"/> records every request's method+URI, in order — later sessions use it to
/// assert batching (`&lt;=100 ids/call`), cache hits (`callCount == 1`), and that an error response
/// is never cached (`callCount == 2` after a retry).
/// </summary>
public sealed class FakeTeamCityHandler : HttpMessageHandler
{
    // A truncated but representative slice of Tomcat's actual HTML 400 page — enough for a test to
    // assert "this is not TeamCity's own JSON error shape" without a full byte-for-byte fixture.
    private const string TomcatBadRequestHtml =
        "<!doctype html><html><head><title>HTTP Status 400 – Bad Request</title></head>" +
        "<body><h1>HTTP Status 400 – Bad Request</h1></body></html>";

    private const int TomcatRequestLineLimit = 4000;

    public List<string> RequestLog { get; } = [];

    private readonly List<Route> _routes = [];

    /// <summary>Fakes <c>GET app/rest/users?locator={dimension}:{value}...</c>. Pass
    /// <paramref name="userId"/> = null for a zero-match (unresolvable identity) response.
    /// <paramref name="userId"/> is rendered as a bare JSON number — TeamCity's real API returns
    /// user ids unquoted, unlike project/buildType ids (verified against a live TeamCity instance,
    /// which caught a real string/int deserialization mismatch this fake had been masking).</summary>
    public FakeTeamCityHandler OnUsers(string locatorDimension, string value, string? userId)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith("/app/rest/users", StringComparison.Ordinal) &&
                   QueryContains(req.RequestUri!, "locator", $"{locatorDimension}:{value}"),
            _ => JsonResponse(userId is null
                ? """{"count":0}"""
                : "{\"count\":1,\"user\":[{\"id\":" + userId + "}]}")));
        return this;
    }

    /// <summary>Fakes <c>GET app/rest/projects/id:{projectId}...</c> with an arbitrary response body.</summary>
    public FakeTeamCityHandler OnProject(string projectId, string responseJson)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith($"/app/rest/projects/id:{projectId}", StringComparison.Ordinal),
            _ => JsonResponse(responseJson)));
        return this;
    }

    /// <summary>Fakes <c>GET app/rest/builds/id:{buildId}...</c>, resolving to the given build type
    /// and project — the shape every G3 pivot needs.</summary>
    public FakeTeamCityHandler OnBuild(string buildId, string buildTypeId, string projectId)
    {
        var json = "{\"id\":" + buildId + ",\"buildTypeId\":\"" + buildTypeId + "\",\"buildType\":{\"id\":\"" +
                   buildTypeId + "\",\"projectId\":\"" + projectId + "\"}}";
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith($"/app/rest/builds/id:{buildId}", StringComparison.Ordinal),
            _ => JsonResponse(json)));
        return this;
    }

    /// <summary>Fakes <c>GET app/rest/buildTypes/id:{buildTypeId}...</c>, resolving to the given
    /// project — the shape the G2 pivot needs. Matches on path only, so this same route also answers
    /// a tool body's own full-detail fetch of the same buildType (with only <c>id</c>/<c>projectId</c>
    /// populated; fine for RBAC-focused tests that don't assert on the rendered markdown body).</summary>
    public FakeTeamCityHandler OnBuildType(string buildTypeId, string projectId)
    {
        var json = "{\"id\":\"" + buildTypeId + "\",\"projectId\":\"" + projectId + "\"}";
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith($"/app/rest/buildTypes/id:{buildTypeId}", StringComparison.Ordinal),
            _ => JsonResponse(json)));
        return this;
    }

    /// <summary>Fakes <c>GET app/rest/vcs-roots/id:{vcsRootId}...</c>, resolving to the given
    /// project — the shape the vcsRoot pivot needs. Matches on path only, same caveat as
    /// <see cref="OnBuildType"/>.</summary>
    public FakeTeamCityHandler OnVcsRoot(string vcsRootId, string projectId)
    {
        var json = "{\"id\":\"" + vcsRootId + "\",\"project\":{\"id\":\"" + projectId + "\"}}";
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith($"/app/rest/vcs-roots/id:{vcsRootId}", StringComparison.Ordinal),
            _ => JsonResponse(json)));
        return this;
    }

    /// <summary>
    /// Fakes <c>GET users/{locator}/permissions...</c>, speaking the same
    /// <c>fields=count,permissionAssignment(project(id))</c> shape live probe 11 requires for any
    /// multi-project check — the fake's original bare <c>{"count":1}</c> shape could not distinguish
    /// "A has 2 grants, B has 0" from "A=1, B=1", the exact ambiguity that probe found.
    /// <paramref name="grants"/> is every <c>(permission, projectId)</c> pair this user holds;
    /// <c>ProjectId: null</c> means a global grant (matches a <c>global:true</c> locator, and also
    /// surfaces as a project-less entry in a multi-project batch response, per probe 8).
    ///
    /// Two traps encode live constraints as test-time failures rather than letting the fake paper
    /// over them: a multi-project locator whose <c>fields=</c> omits <c>permissionAssignment</c>
    /// fails with 400 (an underspecified batch query is genuinely ambiguous, so this fake refuses to
    /// guess); a request URI at or beyond Tomcat's own request-line limit fails with an HTML-shaped
    /// 400 (per probe 10 — the container rejects the request before TeamCity ever parses it).
    /// </summary>
    public FakeTeamCityHandler OnPermissions(string userLocator, IReadOnlyCollection<(string Permission, string? ProjectId)> grants)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith($"/app/rest/users/{userLocator}/permissions", StringComparison.Ordinal),
            req =>
            {
                if (req.RequestUri!.ToString().Length >= TomcatRequestLineLimit)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(TomcatBadRequestHtml, Encoding.UTF8, "text/html"),
                    };
                }

                var query = QueryHelpers.ParseQuery(req.RequestUri!.Query);
                var locator = query.TryGetValue("locator", out var l) ? l.ToString() : string.Empty;
                var fields = query.TryGetValue("fields", out var f) ? f.ToString() : string.Empty;
                var wantsAssignments = fields.Contains("permissionAssignment", StringComparison.Ordinal);

                var permissionMatch = Regex.Match(locator, "permission:([^,)]+)");
                var permission = permissionMatch.Success ? permissionMatch.Groups[1].Value : null;
                var isGlobal = locator.Contains("global:true", StringComparison.Ordinal);
                var batchMatch = Regex.Match(locator, @"project:\(id:\(([^)]*)\)\)");
                var singleMatch = Regex.Match(locator, @"project:\(id:([A-Za-z0-9_.\-]+)\)");

                if (batchMatch.Success)
                {
                    if (!wantsAssignments)
                    {
                        return JsonResponse(
                            """{"message":"Bad Request. A multi-project locator requires permissionAssignment(project(id)) in fields."}""",
                            HttpStatusCode.BadRequest);
                    }

                    var ids = batchMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var grantedIds = permission is { } batchPermission
                        ? ids.Where(id => grants.Contains((batchPermission, (string?)id))).ToArray()
                        : [];
                    var assignments = string.Join(
                        ",", grantedIds.Select(id => "{\"project\":{\"id\":\"" + id + "\"}}"));
                    return JsonResponse(
                        "{\"count\":" + grantedIds.Length + ",\"permissionAssignment\":[" + assignments + "]}");
                }

                // A bare `permission:X` locator — no project:/global:true qualifier — is what
                // GetVisibleProjectSetAsync sends: "every grant of this permission, whatever project
                // (or none, for a global grant) it's on". Distinct from the single-project/global
                // shape below, which asks a yes/no question about one specific scope.
                if (wantsAssignments && !isGlobal && !singleMatch.Success)
                {
                    var matches = permission is { } queryPermission
                        ? grants.Where(g => g.Permission == queryPermission).ToArray()
                        : [];
                    var assignments = string.Join(
                        ",", matches.Select(g => g.ProjectId is null ? "{}" : "{\"project\":{\"id\":\"" + g.ProjectId + "\"}}"));
                    return JsonResponse(
                        "{\"count\":" + matches.Length + ",\"permissionAssignment\":[" + assignments + "]}");
                }

                var projectId = isGlobal ? null : (singleMatch.Success ? singleMatch.Groups[1].Value : null);
                var granted = permission is { } singlePermission && grants.Contains((singlePermission, projectId));

                if (!wantsAssignments)
                {
                    return JsonResponse(granted ? """{"count":1}""" : """{"count":0}""");
                }

                var entry = granted
                    ? (projectId is null ? "{}" : "{\"project\":{\"id\":\"" + projectId + "\"}}")
                    : null;
                return JsonResponse(
                    "{\"count\":" + (granted ? 1 : 0) + ",\"permissionAssignment\":[" + (entry ?? string.Empty) + "]}");
            }));
        return this;
    }

    /// <summary>Fakes any <c>GET</c> whose absolute path starts with <paramref name="pathPrefix"/>
    /// with a fixed response body, regardless of query string — for list/search endpoints
    /// (<c>/app/rest/projects</c>, <c>/app/rest/builds</c>, <c>/app/rest/testOccurrences</c>,
    /// <c>/app/rest/mutes</c>, <c>/app/rest/audit</c>) where the S4 visible-set filtering tests care
    /// about the response shape, not the exact locator/fields query TeamCity received.</summary>
    public FakeTeamCityHandler OnGet(string pathPrefix, string responseJson)
    {
        _routes.Add(new Route(
            req => req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.StartsWith(pathPrefix, StringComparison.Ordinal),
            _ => JsonResponse(responseJson)));
        return this;
    }

    /// <summary>Fakes any <c>GET</c> whose absolute path is exactly <paramref name="path"/> and whose
    /// query string contains <paramref name="queryFragment"/> — for the G5 fan-out case where the
    /// exact same path (e.g. <c>builds/id:100</c>) is hit twice with different <c>fields=</c> values
    /// for two different purposes (a build's own summary vs. its dependency fan-out), which
    /// <see cref="OnGetExactPath"/> alone cannot distinguish.</summary>
    public FakeTeamCityHandler OnGetExactPathWithQuery(string path, string queryFragment, string responseJson)
    {
        _routes.Add(new Route(
            req => req.Method == HttpMethod.Get &&
                   req.RequestUri!.AbsolutePath == path &&
                   req.RequestUri!.Query.Contains(queryFragment, StringComparison.Ordinal),
            _ => JsonResponse(responseJson)));
        return this;
    }

    /// <summary>Fakes any <c>GET</c> whose absolute path is exactly <paramref name="path"/> — unlike
    /// <see cref="OnGet"/>'s prefix match, this is exact, so distinct sibling paths under the same
    /// buildType/build id (e.g. <c>.../buildTypes/id:X/snapshot-dependencies</c> vs.
    /// <c>.../artifact-dependencies</c>) never collide. Used for G5 fan-out pruning tests, where a
    /// single build/buildType id has several distinct dependency-shaped sub-resources to fake.</summary>
    public FakeTeamCityHandler OnGetExactPath(string path, string responseJson)
    {
        _routes.Add(new Route(
            req => req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == path,
            _ => JsonResponse(responseJson)));
        return this;
    }

    /// <summary>
    /// Every request whose URI contains <paramref name="uriFragment"/> fails with
    /// <paramref name="status"/>, permanently. Because routes match in registration order with first
    /// match winning, this route must be registered <c>Before</c> any route it is meant to preempt —
    /// registering it after a matching route means that earlier route always wins and this one never
    /// fires.
    /// </summary>
    public FakeTeamCityHandler OnFailure(string uriFragment, HttpStatusCode status)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.ToString().Contains(uriFragment, StringComparison.Ordinal),
            _ => new HttpResponseMessage(status)));
        return this;
    }

    /// <summary>Like <see cref="OnFailure"/>, but fails only the first <paramref name="times"/>
    /// matching requests, then falls through to whatever route is registered after it (typically a
    /// success route) — needed to prove "an error is never cached": call once, get a failure, retry,
    /// get a real answer, and assert the retry actually reached the network (<c>callCount == 2</c>)
    /// rather than serving a cached fault. Must, like <see cref="OnFailure"/>, be registered before
    /// the route it is meant to temporarily preempt.</summary>
    public FakeTeamCityHandler OnFailureTimes(string uriFragment, HttpStatusCode status, int times)
    {
        var remaining = times;
        _routes.Add(new Route(
            req =>
            {
                if (remaining <= 0 || !req.RequestUri!.ToString().Contains(uriFragment, StringComparison.Ordinal))
                    return false;

                remaining--;
                return true;
            },
            _ => new HttpResponseMessage(status)));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestLog.Add($"{request.Method} {request.RequestUri}");

        foreach (var route in _routes)
        {
            if (route.Match(request))
                return Task.FromResult(route.Respond(request));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                "{\"message\":\"no fake route matched " + request.RequestUri + "\"}", Encoding.UTF8, "application/json"),
        });
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static bool QueryContains(Uri uri, string key, string valueSubstring)
    {
        var query = QueryHelpers.ParseQuery(uri.Query);
        return query.TryGetValue(key, out var value) && value.ToString().Contains(valueSubstring, StringComparison.Ordinal);
    }

    private sealed record Route(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond);
}
