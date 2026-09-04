namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Covers the Session 3 pivot resolver (buildType/build/vcsRoot -&gt; project) end-to-end over HTTP via
/// the fake TeamCity handler — before this session, no test exercised any buildType- or build-scoped
/// tool over HTTP at all. Mirrors <see cref="RbacDenyOracleTests"/>'s pattern: every distinct pivot
/// failure cause (404, upstream error, resolves-to-a-forbidden-project) must render the byte-identical
/// <see cref="ToolGate.DeniedMessage"/>, and a malformed id must never even reach the pivot fetch.
/// </summary>
public class RbacPivotEnforcementTests
{
    private const string Email = "pivot@example.invalid";
    private const string TeamCityUserId = "77";
    private const string ProjectId = "PivotProject";
    private const string BuildTypeId = "MyProject_Build";

    // ---- buildType pivot (G2) ----

    [Fact]
    public async Task BuildTypePivot_Allowed_ReachesTheToolBody()
    {
        var (_, text, requestLog) = await RunAsync(TeamCityToolNames.GetBuildType, "buildTypeId", BuildTypeId, f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnBuildType(BuildTypeId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, ProjectId)]);
        });

        Assert.NotEqual(ToolGate.DeniedMessage, text);
        // Pivot fetch (resolver) + the tool body's own fetch = 2 requests to the same buildType path.
        Assert.Equal(2, requestLog.Count(r => r.Contains($"/app/rest/buildTypes/id:{BuildTypeId}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task BuildTypePivot_NoGrant_Denies_AndNeverReachesTheToolBody()
    {
        var (isError, text, requestLog) = await RunAsync(TeamCityToolNames.GetBuildType, "buildTypeId", BuildTypeId, f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnBuildType(BuildTypeId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
        // Only the resolver's fetch happened — the tool body's own fetch of the same path never ran.
        Assert.Equal(1, requestLog.Count(r => r.Contains($"/app/rest/buildTypes/id:{BuildTypeId}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task BuildTypeId_Malformed_Denies_AndNeverCallsThePivot()
    {
        var (isError, text, requestLog) = await RunAsync(TeamCityToolNames.GetBuildType, "buildTypeId", "Foo,Bar", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
        Assert.DoesNotContain(requestLog, r => r.Contains("/app/rest/buildTypes/", StringComparison.Ordinal));
    }

    // ---- build pivot (G3) ----

    [Fact]
    public async Task BuildPivot_Allowed_ReachesTheToolBody()
    {
        const string buildId = "123";
        var (_, text, requestLog) = await RunAsync(TeamCityToolNames.GetBuildStatus, "buildId", buildId, f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnBuild(buildId, BuildTypeId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, ProjectId)]);
        });

        Assert.NotEqual(ToolGate.DeniedMessage, text);
        Assert.Equal(2, requestLog.Count(r => r.Contains($"/app/rest/builds/id:{buildId}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task BuildPivot_NoGrant_Denies_AndNeverReachesTheToolBody()
    {
        const string buildId = "123";
        var (isError, text, requestLog) = await RunAsync(TeamCityToolNames.GetBuildStatus, "buildId", buildId, f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnBuild(buildId, BuildTypeId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
        Assert.Equal(1, requestLog.Count(r => r.Contains($"/app/rest/builds/id:{buildId}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task BuildId_Malformed_Denies_AndNeverCallsThePivot()
    {
        var (isError, text, requestLog) = await RunAsync(TeamCityToolNames.GetBuildStatus, "buildId", "abc123", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
        Assert.DoesNotContain(requestLog, r => r.Contains("/app/rest/builds/", StringComparison.Ordinal));
    }

    // ---- vcsRoot pivot ----

    [Fact]
    public async Task VcsRootPivot_Allowed_ReachesTheToolBody()
    {
        const string vcsRootId = "MyVcsRoot";
        var (_, text, requestLog) = await RunAsync(TeamCityToolNames.GetVcsRoot, "vcsRootId", vcsRootId, f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnVcsRoot(vcsRootId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, ProjectId)]);
        });

        Assert.NotEqual(ToolGate.DeniedMessage, text);
        Assert.Equal(2, requestLog.Count(r => r.Contains($"/app/rest/vcs-roots/id:{vcsRootId}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task VcsRootPivot_NoGrant_Denies_AndNeverReachesTheToolBody()
    {
        const string vcsRootId = "MyVcsRoot";
        var (isError, text, requestLog) = await RunAsync(TeamCityToolNames.GetVcsRoot, "vcsRootId", vcsRootId, f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnVcsRoot(vcsRootId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
        Assert.Equal(1, requestLog.Count(r => r.Contains($"/app/rest/vcs-roots/id:{vcsRootId}", StringComparison.Ordinal)));
    }

    // ---- deny-oracle: pivot 404, pivot upstream error, and resolves-to-forbidden-project must be
    // byte-identical — the pivot itself must never become a resource-existence oracle. ----

    public static IEnumerable<object[]> PivotDenyScenarios()
    {
        yield return new object[] { "pivot_buildtype_unresolved_404", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnFailure($"/app/rest/buildTypes/id:{BuildTypeId}", HttpStatusCode.NotFound);
        }) };

        yield return new object[] { "pivot_upstream_error_500", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnFailure($"/app/rest/buildTypes/id:{BuildTypeId}", HttpStatusCode.InternalServerError);
        }) };

        yield return new object[] { "pivot_resolves_to_forbidden_project", (Action<TeamCityFakeFactory>)(f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnBuildType(BuildTypeId, ProjectId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
        }) };
    }

    [Theory]
    [MemberData(nameof(PivotDenyScenarios))]
    public async Task EveryPivotDenyCause_RendersTheByteIdenticalDeniedMessage(
        string scenarioName, Action<TeamCityFakeFactory> configure)
    {
        var (isError, text, _) = await RunAsync(TeamCityToolNames.GetBuildType, "buildTypeId", BuildTypeId, configure);

        Assert.True(isError, $"[{scenarioName}] must be a JSON-RPC result with isError:true.");
        Assert.Equal(ToolGate.DeniedMessage, text);
    }

    [Fact]
    public async Task AllPivotDenyScenarios_ProduceExactlyOneDistinctBody()
    {
        var bodies = new List<string>();

        foreach (var scenarioCase in PivotDenyScenarios())
        {
            var configure = (Action<TeamCityFakeFactory>)scenarioCase[1];
            var (_, text, _) = await RunAsync(TeamCityToolNames.GetBuildType, "buildTypeId", BuildTypeId, configure);
            bodies.Add(text);
        }

        Assert.Equal(3, bodies.Count);
        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }

    private static async Task<(bool IsError, string Text, IReadOnlyList<string> RequestLog)> RunAsync(
        string toolName, string argumentName, string argumentValue, Action<TeamCityFakeFactory> configure)
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
                  "params": { "name": "{{toolName}}", "arguments": { "{{argumentName}}": "{{argumentValue}}" } }
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
