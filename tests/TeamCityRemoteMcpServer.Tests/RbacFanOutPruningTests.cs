namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Covers Session 5's G5 fan-out pruning — <c>teamcity_get_build_dependency_tree</c> and
/// <c>teamcity_get_build_type_dependency_graph</c> moved out of
/// <see cref="GateEnforcement.DeferredToLaterSession"/> this session. The root resource was already
/// gated via the Session 3 pivot (covered by <see cref="RbacPivotEnforcementTests"/>); what's new here
/// is that a discovered fan-out node outside the root's own project gets pruned, along with every
/// edge touching it, per the locked "pruning, not re-rooting" decision for these two tools (distinct
/// from <c>teamcity_get_project_hierarchy</c>'s re-rooting). <c>depth:1</c> is used throughout so a
/// pruned child's own further fan-out never needs to be faked — pruning happens after the whole walk
/// completes, so a depth-1 walk still proves the prune, just with less HTTP scaffolding.
/// </summary>
public class RbacFanOutPruningTests
{
    private const string Email = "fanout@example.invalid";
    private const string TeamCityUserId = "91";
    private const string VisibleProject = "VisibleProject";
    private const string HiddenProject = "HiddenProject";

    // ---- teamcity_get_build_dependency_tree ----

    [Fact]
    public async Task GetBuildDependencyTree_PrunesAChildInAHiddenProject_AndEveryEdgeToIt()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetBuildDependencyTree, """{"buildId":"100","depth":1}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);

                f.Handler.OnGetExactPathWithQuery("/app/rest/builds/id:100", "snapshot-dependencies", """
                    {"snapshot-dependencies":{"build":[
                        {"id":200,"number":"5","status":"SUCCESS","state":"finished","buildTypeId":"ChildBt",
                         "buildType":{"id":"ChildBt","name":"Child","projectId":"HiddenProject"}}
                    ]},"artifact-dependencies":{"build":[]}}
                    """);
                f.Handler.OnBuild("100", "RootBt", VisibleProject);
            });

        Assert.False(isError);
        Assert.DoesNotContain("HiddenProject", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Child", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id:200", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBuildDependencyTree_PrunedToEmpty_IsByteIdenticalToGenuinelyNoDependencies()
    {
        var (_, pruned) = await RunAsync(
            TeamCityToolNames.GetBuildDependencyTree, """{"buildId":"100","depth":1}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);

                f.Handler.OnGetExactPathWithQuery("/app/rest/builds/id:100", "snapshot-dependencies", """
                    {"snapshot-dependencies":{"build":[
                        {"id":200,"number":"5","status":"SUCCESS","state":"finished","buildTypeId":"ChildBt",
                         "buildType":{"id":"ChildBt","name":"Child","projectId":"HiddenProject"}}
                    ]},"artifact-dependencies":{"build":[]}}
                    """);
                f.Handler.OnBuild("100", "RootBt", VisibleProject);
            });

        var (_, genuinelyEmpty) = await RunAsync(
            TeamCityToolNames.GetBuildDependencyTree, """{"buildId":"100","depth":1}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
                f.Handler.OnGetExactPathWithQuery("/app/rest/builds/id:100", "snapshot-dependencies", "{}");
                f.Handler.OnBuild("100", "RootBt", VisibleProject);
            });

        Assert.Equal(genuinelyEmpty, pruned);
    }

    [Fact]
    public async Task GetBuildDependencyTree_VisibleChild_IsRenderedNormally()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetBuildDependencyTree, """{"buildId":"100","depth":1}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}",
                    [(TeamCityPermission.ViewProject, VisibleProject)]);

                f.Handler.OnGetExactPathWithQuery("/app/rest/builds/id:100", "snapshot-dependencies", """
                    {"snapshot-dependencies":{"build":[
                        {"id":200,"number":"5","status":"SUCCESS","state":"finished","buildTypeId":"ChildBt",
                         "buildType":{"id":"ChildBt","name":"Child","projectId":"VisibleProject"}}
                    ]},"artifact-dependencies":{"build":[]}}
                    """);
                f.Handler.OnBuild("100", "RootBt", VisibleProject);
            });

        Assert.False(isError);
        Assert.Contains("Child", text, StringComparison.Ordinal);
        Assert.Contains("id:200", text, StringComparison.Ordinal);
    }

    // ---- teamcity_get_build_type_dependency_graph ----

    [Fact]
    public async Task GetBuildTypeDependencyGraph_PrunesADependencyInAHiddenProject_AndEveryEdgeToIt()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetBuildTypeDependencyGraph,
            """{"buildTypeId":"RootBt","depth":1,"only":"dependencies"}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);

                f.Handler.OnGetExactPath("/app/rest/buildTypes/id:RootBt/snapshot-dependencies", """
                    {"count":1,"snapshot-dependency":[
                        {"id":"dep1","source-buildType":{"id":"ChildBt","name":"Child","projectId":"HiddenProject"}}
                    ]}
                    """);
                f.Handler.OnGetExactPath("/app/rest/buildTypes/id:RootBt/artifact-dependencies", """{"count":0}""");
                f.Handler.OnBuildType("RootBt", VisibleProject);
            });

        Assert.False(isError);
        Assert.DoesNotContain("HiddenProject", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ChildBt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBuildTypeDependencyGraph_PrunedToEmpty_IsByteIdenticalToGenuinelyNoDependencies()
    {
        var (_, pruned) = await RunAsync(
            TeamCityToolNames.GetBuildTypeDependencyGraph,
            """{"buildTypeId":"RootBt","depth":1,"only":"dependencies"}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);

                f.Handler.OnGetExactPath("/app/rest/buildTypes/id:RootBt/snapshot-dependencies", """
                    {"count":1,"snapshot-dependency":[
                        {"id":"dep1","source-buildType":{"id":"ChildBt","name":"Child","projectId":"HiddenProject"}}
                    ]}
                    """);
                f.Handler.OnGetExactPath("/app/rest/buildTypes/id:RootBt/artifact-dependencies", """{"count":0}""");
                f.Handler.OnBuildType("RootBt", VisibleProject);
            });

        var (_, genuinelyEmpty) = await RunAsync(
            TeamCityToolNames.GetBuildTypeDependencyGraph,
            """{"buildTypeId":"RootBt","depth":1,"only":"dependencies"}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
                f.Handler.OnGetExactPath("/app/rest/buildTypes/id:RootBt/snapshot-dependencies", """{"count":0}""");
                f.Handler.OnGetExactPath("/app/rest/buildTypes/id:RootBt/artifact-dependencies", """{"count":0}""");
                f.Handler.OnBuildType("RootBt", VisibleProject);
            });

        Assert.Equal(genuinelyEmpty, pruned);
    }

    // ---- teamcity_get_build_tests chain parts ----

    [Fact]
    public async Task GetBuildTests_PrunesAChainPartInAHiddenProject_FromTheChainPartsTable()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetBuildTests, """{"buildId":"100"}""", f =>
            {
                f.Handler.OnUsers("email", Email, TeamCityUserId);
                f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);

                f.Handler.OnGetExactPathWithQuery("/app/rest/builds/id:100", "testOccurrences", """
                    {"id":100,"number":"5","composite":true,"buildType":{"id":"RootBt","name":"Root"},
                     "testOccurrences":{"count":0,"passed":0,"failed":0,"ignored":0,"muted":0,"newFailed":0},
                     "snapshot-dependencies":{"build":[
                        {"id":200,"number":"5","status":"SUCCESS","composite":false,
                         "buildType":{"id":"ChildBt","name":"Child","projectId":"HiddenProject"}}
                     ]}}
                    """);
                f.Handler.OnGet("/app/rest/testOccurrences", """{"testOccurrence":[]}""");
                f.Handler.OnBuild("100", "RootBt", VisibleProject);
            });

        Assert.False(isError);
        Assert.DoesNotContain("HiddenProject", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Child", text, StringComparison.Ordinal);
        Assert.Contains("No direct snapshot-dependency sub-builds found.", text, StringComparison.Ordinal);
    }

    private static async Task<(bool IsError, string Text)> RunAsync(
        string toolName, string argumentsJson, Action<TeamCityFakeFactory> configure)
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
                  "params": { "name": "{{toolName}}", "arguments": {{argumentsJson}} }
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

        return ExtractToolResult(body);
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
