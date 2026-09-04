namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Proves the locked "no resource-existence oracle" decision holds against a real gate, not just
/// against the fake <c>AlwaysDenyGate</c>: six structurally different deny causes (a real
/// <c>count:0</c>, an upstream 400/404/500, and the same two shapes at identity resolution) must all
/// render the byte-identical response — collected and compared against *each other*
/// (<c>Assert.Single(bodies.Distinct())</c>), not each asserted against a constant that could itself
/// start leaking a distinction. Each scenario also proves the deny happened *before* the project was
/// ever fetched, and that the transport-level shape is a normal JSON-RPC result with
/// <c>isError: true</c>, never a protocol-level error (which would itself be a distinguishing oracle).
/// </summary>
public class RbacDenyOracleTests
{
    private const string Email = "dora@example.invalid";
    private const string TeamCityUserId = "55";
    private const string ProjectId = "MyProject";

    public static IEnumerable<object[]> DenyScenarios()
    {
        yield return new object[] { "count_zero", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
        }) };

        yield return new object[] { "permission_400", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnFailure("/permissions", HttpStatusCode.BadRequest);
        }) };

        yield return new object[] { "permission_404", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnFailure("/permissions", HttpStatusCode.NotFound);
        }) };

        yield return new object[] { "permission_500", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnFailure("/permissions", HttpStatusCode.InternalServerError);
        }) };

        yield return new object[] { "identity_count_zero", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, userId: null);
        }) };

        yield return new object[] { "identity_404", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnFailure("/app/rest/users?", HttpStatusCode.NotFound);
        }) };
    }

    [Theory]
    [MemberData(nameof(DenyScenarios))]
    public async Task EveryDenyCause_RendersAnErrorResult_AndNeverFetchesTheProject(
        string scenarioName, Action<TeamCityFakeFactory> configure)
    {
        var (isError, text, requestLog) = await RunScenarioAsync(configure);

        Assert.True(isError, $"[{scenarioName}] must be a JSON-RPC result with isError:true, never a protocol error.");
        Assert.Equal(ToolGate.DeniedMessage, text);
        Assert.DoesNotContain(requestLog, r => r.Contains("/app/rest/projects/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllScenarios_ProduceExactlyOneDistinctBody()
    {
        var bodies = new List<string>();

        foreach (var scenarioCase in DenyScenarios())
        {
            var configure = (Action<TeamCityFakeFactory>)scenarioCase[1];
            var (_, text, _) = await RunScenarioAsync(configure);
            bodies.Add(text);
        }

        Assert.Equal(6, bodies.Count);
        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }

    private static async Task<(bool IsError, string Text, IReadOnlyList<string> RequestLog)> RunScenarioAsync(
        Action<TeamCityFakeFactory> configure)
    {
        using var factory = new TeamCityFakeFactory();
        using var mcpAuth = new McpAuthTestConfigBuilder();
        mcpAuth.Apply(factory);
        factory.WithRbacEnabled(auditOnly: false);
        configure(factory);

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

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"transport itself must succeed: {body}");

        var (isError, text) = ExtractToolResult(body);
        return (isError, text, factory.Handler.RequestLog.ToArray());
    }

    /// <summary>
    /// A counter-intuitive but load-bearing invariant: once the gate allows, TeamCity's own
    /// <c>404</c> for the same project id becomes structurally unreachable, because an allow implies
    /// <c>count &gt;= 1</c> at that exact project id, which implies the project exists. This test
    /// deliberately breaks that invariant (grants the permission, then 404s the project fetch anyway)
    /// to pin what that inconsistency looks like — <c>ProjectTools.GetProject</c>'s own
    /// <c>"ERROR: Project with ID '...' was not found."</c>, textually distinct from
    /// <see cref="ToolGate.DeniedMessage"/>. If this string is ever observed on a genuinely allowed
    /// path in production, the check and the fetch disagreed about which resource they were talking
    /// about (e.g. a case-sensitivity divergence) — this test exists so that failure mode has a name.
    /// </summary>
    [Fact]
    public async Task AllowedPath_With404FromTeamCity_ProducesTheToolsOwnNotFoundMessage_DistinctFromTheRbacDeniedMessage()
    {
        var (_, text, _) = await RunScenarioAsync(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, ProjectId)]);
            f.Handler.OnFailure($"/app/rest/projects/id:{ProjectId}", HttpStatusCode.NotFound);
        });

        Assert.Equal($"ERROR: Project with ID '{ProjectId}' was not found.", text);
        Assert.NotEqual(ToolGate.DeniedMessage, text);
    }

    private static (bool IsError, string Text) ExtractToolResult(string body)
    {
        var dataLine = body
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal));

        var json = dataLine is null ? body : dataLine["data:".Length..].Trim();

        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("result");
        var isError = result.TryGetProperty("isError", out var errorProp) && errorProp.GetBoolean();
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        return (isError, text);
    }
}
