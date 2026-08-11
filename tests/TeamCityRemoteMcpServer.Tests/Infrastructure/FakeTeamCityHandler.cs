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
    public List<string> RequestLog { get; } = [];

    private readonly List<Route> _routes = [];

    /// <summary>Fakes <c>GET app/rest/users?locator={dimension}:{value}...</c>. Pass
    /// <paramref name="userId"/> = null for a zero-match (unresolvable identity) response.</summary>
    public FakeTeamCityHandler OnUsers(string locatorDimension, string value, string? userId)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith("/app/rest/users", StringComparison.Ordinal) &&
                   QueryContains(req.RequestUri!, "locator", $"{locatorDimension}:{value}"),
            _ => JsonResponse(userId is null
                ? """{"count":0}"""
                : "{\"count\":1,\"user\":[{\"id\":\"" + userId + "\"}]}")));
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

    /// <summary>Fakes <c>GET users/{locator}/permissions...</c>. <paramref name="allow"/> lists the
    /// permission ids this user holds (matched against the locator's <c>permission:</c> dimension);
    /// anything else in the locator yields <c>count: 0</c>.</summary>
    public FakeTeamCityHandler OnPermissions(string userLocator, IReadOnlyCollection<string> allow)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.AbsolutePath.EndsWith($"/app/rest/users/{userLocator}/permissions", StringComparison.Ordinal),
            req =>
            {
                var query = QueryHelpers.ParseQuery(req.RequestUri!.Query);
                var locator = query.TryGetValue("locator", out var l) ? l.ToString() : string.Empty;
                var permissionMatch = Regex.Match(locator, "permission:([^,)]+)");
                var granted = !permissionMatch.Success || allow.Contains(permissionMatch.Groups[1].Value);
                return JsonResponse(granted ? """{"count":1}""" : """{"count":0}""");
            }));
        return this;
    }

    /// <summary>Any request whose URI contains <paramref name="uriFragment"/> fails with
    /// <paramref name="status"/> instead of matching a route registered before it.</summary>
    public FakeTeamCityHandler OnFailure(string uriFragment, HttpStatusCode status)
    {
        _routes.Add(new Route(
            req => req.RequestUri!.ToString().Contains(uriFragment, StringComparison.Ordinal),
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

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static bool QueryContains(Uri uri, string key, string valueSubstring)
    {
        var query = QueryHelpers.ParseQuery(uri.Query);
        return query.TryGetValue(key, out var value) && value.ToString().Contains(valueSubstring, StringComparison.Ordinal);
    }

    private sealed record Route(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond);
}
