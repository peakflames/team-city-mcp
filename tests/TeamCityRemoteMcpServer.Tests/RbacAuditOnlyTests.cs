namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// The regression test for the shadow-mode defect: before this session, <c>AuditOnly</c> computed
/// <c>Decision</c> from the effective (post-<c>AuditOnly</c>) allow/deny, so shadow mode logged every
/// would-be deny as an <c>Allow</c> — producing none of the data it exists to produce. Also covers
/// that the audit record survives a throwing tool body, proving <c>Record(...)</c> really does run
/// before <c>next(...)</c>, not just look like it does from reading the source.
/// </summary>
public class RbacAuditOnlyTests : IDisposable
{
    private const string Email = "carol@example.invalid";
    private const string TeamCityUserId = "99";

    private readonly LogCapture _capture = new();
    private readonly TeamCityFakeFactory _factory = new();
    private readonly McpAuthTestConfigBuilder _mcpAuth = new();

    public void Dispose()
    {
        _factory.Dispose();
        _capture.Dispose();
        _mcpAuth.Dispose();
    }

    [Fact]
    public async Task AuditOnly_True_DenyingGate_StillRunsTheToolBody_AndLogsTheTrueDenyVerdict()
    {
        _mcpAuth.Apply(_factory);
        _factory.WithRbacEnabled(auditOnly: true);
        _factory.WithPostAuthServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPermissionGate>(new AlwaysDenyGate())));
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(
            "MyProject",
            """{"id":"MyProject","name":"My Project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""");

        var body = await CallToolAsync("teamcity_get_project", """{"projectId":"MyProject"}""");

        Assert.Contains("My Project", body, StringComparison.Ordinal);
        Assert.Contains(_capture.Lines, l => l.Contains("decision=Deny", StringComparison.Ordinal));
        Assert.Contains(_capture.Lines, l => l.Contains("blocked=False", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuditOnly_False_DenyingGate_BlocksTheCall_AndReturnsTheDeniedMessage()
    {
        _mcpAuth.Apply(_factory);
        _factory.WithRbacEnabled(auditOnly: false);
        _factory.WithPostAuthServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPermissionGate>(new AlwaysDenyGate())));
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);

        var body = await CallToolAsync("teamcity_get_project", """{"projectId":"MyProject"}""");

        Assert.Contains(ToolGate.DeniedMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(_factory.Handler.RequestLog, r => r.Contains("/app/rest/projects/", StringComparison.Ordinal));
        Assert.Contains(_capture.Lines, l => l.Contains("decision=Deny", StringComparison.Ordinal));
        Assert.Contains(_capture.Lines, l => l.Contains("blocked=True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThrowingToolBody_StillEmitsItsAuditRecord()
    {
        _mcpAuth.Apply(_factory);
        _factory.WithRbacEnabled();

        // teamcity_server_info is NeverGated, so neither identity resolution nor the gate decision
        // ever surfaces this broken registration (both catch broadly and fail closed on their own
        // use of it) — the only place it can crash is the tool body's own un-caught preamble,
        // proving the audit record really is written before `next(...)` runs, not just after it
        // succeeds.
        _factory.WithPostAuthServices(services => services.Replace(
            ServiceDescriptor.Scoped<ITeamCityClientFactory>(_ => throw new InvalidOperationException("tool body's client factory exploded"))));

        try
        {
            await CallToolAsync(TeamCityToolNames.ServerInfo, "{}");
        }
        catch
        {
            // Acceptable — whether the MCP SDK's own top-level handling turns an unhandled tool
            // exception into a JSON-RPC error response or a transport failure is not this test's
            // concern. Only the audit record's survival is.
        }

        Assert.Contains(_capture.Lines, l => l.Contains("MCP access audit", StringComparison.Ordinal));
        Assert.Contains(_capture.Lines, l => l.Contains("decision=Allow", StringComparison.Ordinal));
    }

    private async Task<string> CallToolAsync(string toolName, string argumentsJson)
    {
        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read], email: Email);

        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"" +
                   toolName + "\",\"arguments\":" + argumentsJson + "}}";

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}
