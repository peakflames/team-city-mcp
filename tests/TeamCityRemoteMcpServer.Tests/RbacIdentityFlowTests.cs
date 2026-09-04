namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// The session's headline result. Proves four things in one round trip: McpAuth populates
/// <c>MessageContext.User</c>; <see cref="RbacIdentityFilter"/> reads the configured identity
/// claim and resolves it to a TeamCity user id; the <c>AsyncLocal</c>-backed
/// <see cref="IRbacCallContextAccessor"/> survives being read from a brand-new sibling DI scope
/// (the same relationship a gated tool body's own <c>CreateAsyncScope()</c> has to the filter's);
/// and the gate receives the correct tool name and resource argument, resolved via
/// <see cref="ToolResourcePermissionMap"/>.
///
/// If this test fails, the documented fallback is threading a
/// <c>RequestContext&lt;CallToolRequestParams&gt;</c> parameter through the 32 tool signatures
/// instead of relying on the AsyncLocal handoff — see the tracker for the decision record.
/// </summary>
public class RbacIdentityFlowTests : IDisposable
{
    private const string Email = "alice@example.invalid";
    private const string TeamCityUserId = "42";
    private const string ProjectId = "MyProject";

    private readonly TeamCityFakeFactory _factory;
    private readonly McpAuthTestConfigBuilder _mcpAuth = new();
    private RecordingGate? _gate;

    public RbacIdentityFlowTests()
    {
        _factory = new TeamCityFakeFactory();
        _mcpAuth.Apply(_factory);
        _factory.WithRbacEnabled();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _mcpAuth.Dispose();
    }

    [Fact]
    public async Task CallToolFilter_ResolvesIdentityClaim_AndFlowsIntoToolBodyAcrossNestedScope()
    {
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(
            ProjectId,
            """{"id":"MyProject","name":"My Project","description":"A test project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""");

        // Runs after AddRbac's own Replace() call — the only seam that can win against it. The
        // factory delegate receives the root IServiceProvider (singleton resolution semantics),
        // which is exactly what RecordingGate needs to create independent sibling scopes.
        _factory.WithPostAuthServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPermissionGate>(sp => _gate = new RecordingGate(sp))));

        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read], email: Email);

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "tools/call",
                  "params": { "name": "teamcity_get_project", "arguments": { "projectId": "MyProject" } }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        Assert.Contains("My Project", body, StringComparison.Ordinal);

        Assert.NotNull(_gate);
        var call = Assert.Single(_gate!.Calls);
        Assert.Equal("teamcity_get_project", call.ToolName);
        Assert.Equal(ProjectId, call.ProjectId);
        Assert.Equal(TeamCityPermission.ViewProject, call.Permission);
        Assert.Equal(TeamCityUserId, call.Identity);

        // Proves the AsyncLocal handoff: read via a scope RecordingGate created independently of
        // the filter's own resolved services, and it still carries the exact context the filter set.
        Assert.NotNull(call.ObservedViaSiblingScope);
        Assert.Equal("teamcity_get_project", call.ObservedViaSiblingScope!.ToolName);
        Assert.Equal(ResourceKind.Project, call.ObservedViaSiblingScope.ResourceKind);
        Assert.Equal(ProjectId, call.ObservedViaSiblingScope.Resource);
        Assert.Equal(Email, call.ObservedViaSiblingScope.IdentityClaimValue);
        Assert.Equal(TeamCityUserId, call.ObservedViaSiblingScope.TeamCityUserId);
    }
}
