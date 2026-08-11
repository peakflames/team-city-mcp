namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Extends <c>LogRedactionTests</c>' rule to the RBAC path: a caller's bearer token, the shared
/// TeamCity PAT, and any raw <c>Authorization</c> header value must never reach a log line, at
/// Verbose, including inside the new <see cref="AccessAuditRecord"/> path — even though the audit
/// record deliberately does log the caller's identity claim value and resolved TeamCity user id
/// (that is this feature's stated purpose, not a leak).
/// </summary>
public class RbacLogRedactionTests : IDisposable
{
    private const string Email = "bob@example.invalid";
    private const string TeamCityUserId = "7";
    private const string TeamCityAccessToken = "super-secret-teamcity-pat";

    private readonly LogCapture _capture = new();
    private readonly TeamCityFakeFactory _factory;
    private readonly McpAuthTestConfigBuilder _mcpAuth = new();

    public RbacLogRedactionTests()
    {
        _factory = new TeamCityFakeFactory();
        _factory.With("TEAM_CITY_ACCESS_TOKEN", TeamCityAccessToken);
        _mcpAuth.Apply(_factory);
        _factory.WithRbacEnabled();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _mcpAuth.Dispose();
        _capture.Dispose();
    }

    [Fact]
    public async Task GatedToolCall_NeverLogsTheBearerTokenOrTheTeamCityPat()
    {
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, "MyProject")]);
        _factory.Handler.OnProject(
            "MyProject",
            """{"id":"MyProject","name":"My Project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""");

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
        Assert.True(response.IsSuccessStatusCode);

        // Proves the call was actually allowed (not a JSON-RPC error result, which is still an HTTP
        // 200) — without this, a permission-check regression that denies every call would still
        // leave every assertion below passing vacuously.
        Assert.Contains("My Project", body, StringComparison.Ordinal);

        // Positive control — without this, a wiring regression that silently stops capturing logs
        // would make every assertion below pass vacuously.
        Assert.Contains(_capture.Lines, line => line.Contains("MCP access audit", StringComparison.Ordinal));

        Assert.DoesNotContain(token, _capture.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ey", _capture.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(TeamCityAccessToken, _capture.Text, StringComparison.Ordinal);

        // The identity claim value and resolved TeamCity user id ARE expected in the audit line —
        // that is the feature's stated purpose (attribution), not a leak.
        Assert.Contains(Email, _capture.Text, StringComparison.Ordinal);
        Assert.Contains(TeamCityUserId, _capture.Text, StringComparison.Ordinal);
    }
}
