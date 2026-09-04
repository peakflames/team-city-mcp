namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Proves the permission and identity caches are actually wired into the live
/// <c>tools/call</c> pipeline — <see cref="TtlCacheTests"/> already proves the cache mechanism in
/// isolation (TTL expiry, single-flight, never-cache-errors); this class proves
/// <see cref="TeamCityPermissionGate"/> and <see cref="Caching.CachingIdentityResolver"/> actually use
/// it, via <see cref="FakeTeamCityHandler.RequestLog"/> call counts.
/// </summary>
public class RbacCachingIntegrationTests : IDisposable
{
    private const string Email = "faye@example.invalid";
    private const string TeamCityUserId = "63";
    private const string ProjectId = "MyProject";

    private readonly FakeTimeProvider _time = new();
    private readonly TeamCityFakeFactory _factory = new();
    private readonly McpAuthTestConfigBuilder _mcpAuth = new();

    public RbacCachingIntegrationTests()
    {
        _mcpAuth.Apply(_factory);
        _factory.WithRbacEnabled();
        _factory.WithFakeTime(_time);
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, ProjectId)]);
        _factory.Handler.OnProject(
            ProjectId,
            """{"id":"MyProject","name":"My Project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""");
    }

    public void Dispose()
    {
        _factory.Dispose();
        _mcpAuth.Dispose();
    }

    [Fact]
    public async Task TwoIdenticalCalls_ProduceExactlyOnePermissionsRequest()
    {
        await CallGetProjectAsync();
        await CallGetProjectAsync();

        Assert.Equal(1, PermissionsRequestCount());
    }

    [Fact]
    public async Task TwoIdenticalCalls_ProduceExactlyOneIdentityLookupRequest()
    {
        await CallGetProjectAsync();
        await CallGetProjectAsync();

        Assert.Equal(1, UsersRequestCount());
    }

    [Fact]
    public async Task AFailedPermissionsCall_IsNeverCached_TheRetryReachesTheNetworkAgain()
    {
        // Registered before OnPermissions in the constructor's route list would matter, but here we
        // add a fresh factory-level failure route that fails only the first match, per OnFailure's
        // first-registration-wins rule — this must be added to a *new* handler pipeline (before the
        // success route in the constructor already registered), so build a dedicated factory here.
        using var factory = new TeamCityFakeFactory();
        using var mcpAuth = new McpAuthTestConfigBuilder();
        mcpAuth.Apply(factory).WithRbacEnabled().WithFakeTime(_time);
        factory.Handler.OnFailureTimes("/permissions", HttpStatusCode.InternalServerError, times: 1);
        factory.Handler.OnUsers("email", Email, TeamCityUserId);
        factory.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, ProjectId)]);
        factory.Handler.OnProject(
            ProjectId,
            """{"id":"MyProject","name":"My Project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""");

        await CallGetProjectAsync(factory, mcpAuth); // fails (500), denied, nothing cached
        await CallGetProjectAsync(factory, mcpAuth); // must reach the network again, not serve a cached fault

        Assert.Equal(2, factory.Handler.RequestLog.Count(r => r.Contains("/permissions", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AfterTheTtlExpires_TheNextCallReQueries()
    {
        _factory.With("Rbac:PermissionCacheTtlSeconds", "120");

        await CallGetProjectAsync();
        Assert.Equal(1, PermissionsRequestCount());

        _time.Advance(TimeSpan.FromSeconds(121));
        await CallGetProjectAsync();

        Assert.Equal(2, PermissionsRequestCount());
    }

    [Fact]
    public async Task ConcurrentIdenticalCalls_ShareTheCache_FarFewerRequestsThanCalls()
    {
        const int concurrentCalls = 20;

        var tasks = Enumerable.Range(0, concurrentCalls).Select(_ => CallGetProjectAsync());
        await Task.WhenAll(tasks);

        // The strict single-flight guarantee (exactly one upstream call, ever) is proven
        // deterministically in TtlCacheTests via a controlled gate. Here, against a real ASP.NET
        // pipeline with no artificial delay, the assertion is looser but still meaningful: without
        // the cache wired in at all, this would be 20 requests, not a small handful.
        Assert.True(
            PermissionsRequestCount() < concurrentCalls,
            $"Expected substantial cache sharing across {concurrentCalls} concurrent identical calls, " +
            $"got {PermissionsRequestCount()} upstream /permissions requests.");
    }

    private int PermissionsRequestCount() =>
        _factory.Handler.RequestLog.Count(r => r.Contains("/permissions", StringComparison.Ordinal));

    private int UsersRequestCount() =>
        _factory.Handler.RequestLog.Count(r => r.Contains("/app/rest/users?", StringComparison.Ordinal));

    private Task CallGetProjectAsync() => CallGetProjectAsync(_factory, _mcpAuth);

    private static async Task CallGetProjectAsync(TeamCityFakeFactory factory, McpAuthTestConfigBuilder mcpAuth)
    {
        var token = mcpAuth.CreateAccessToken([OAuthScopes.Read], email: Email);

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $$"""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "tools/call",
                  "params": { "name": "teamcity_get_project", "arguments": { "projectId": "{{ProjectId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.SendAsync(request);
    }
}
