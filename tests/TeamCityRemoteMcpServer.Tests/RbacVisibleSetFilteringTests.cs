namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Covers the Session 4 visible-set filtering added to the 5 G4 cross-project tools
/// (<c>list_projects</c>, <c>get_project_hierarchy</c>, <c>search_builds</c>, <c>get_test_history</c>,
/// <c>list_mutes</c>) plus <c>get_audit_log</c>'s global-permission gate — the first session where any
/// of these six tools actually enforces anything beyond "identity resolved". Before this session
/// they were all <c>GateEnforcement.DeferredToLaterSession</c> and allowed every result through
/// unfiltered.
/// </summary>
public class RbacVisibleSetFilteringTests
{
    private const string Email = "vera@example.invalid";
    private const string TeamCityUserId = "88";
    private const string VisibleProject = "VisibleProject";
    private const string HiddenProject = "HiddenProject";

    // ---- teamcity_list_projects ----

    [Fact]
    public async Task ListProjects_FiltersOutInvisibleProjects_AndTheTotalFooterReflectsThePostFilterCount()
    {
        var (isError, text) = await RunAsync(TeamCityToolNames.ListProjects, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/projects", $$"""
                {"count":2,"project":[
                    {"id":"{{VisibleProject}}","name":"Visible","parentProjectId":"_Root"},
                    {"id":"{{HiddenProject}}","name":"Hidden","parentProjectId":"_Root"}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.Contains(VisibleProject, text, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenProject, text, StringComparison.Ordinal);
        Assert.Contains("**Total:** 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListProjects_RendersAnInvisibleParentIdAsADash_NotTheHiddenId()
    {
        const string hiddenParentId = "HiddenParent";
        const string visibleChildId = "VisibleChild";

        var (isError, text) = await RunAsync(TeamCityToolNames.ListProjects, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, visibleChildId)]);
            f.Handler.OnGet("/app/rest/projects", $$"""
                {"count":2,"project":[
                    {"id":"{{hiddenParentId}}","name":"Hidden Parent","parentProjectId":"_Root"},
                    {"id":"{{visibleChildId}}","name":"Visible Child","parentProjectId":"{{hiddenParentId}}"}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.Contains(visibleChildId, text, StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenParentId, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListProjects_GlobalGrant_IsByteIdenticalToRbacOff()
    {
        const string rawProjectsJson = """
            {"count":2,"project":[
                {"id":"ProjA","name":"A","parentProjectId":"_Root"},
                {"id":"ProjB","name":"B","parentProjectId":"_Root"}
            ]}
            """;

        using var globalGrantFactory = new TeamCityFakeFactory();
        using var mcpAuth = new McpAuthTestConfigBuilder();
        mcpAuth.Apply(globalGrantFactory);
        globalGrantFactory.WithRbacEnabled();
        globalGrantFactory.Handler.OnUsers("email", Email, TeamCityUserId);
        globalGrantFactory.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, null)]);
        globalGrantFactory.Handler.OnGet("/app/rest/projects", rawProjectsJson);
        var withGlobalGrant = await CallToolAsync(globalGrantFactory, mcpAuth, TeamCityToolNames.ListProjects, "{}");

        using var rbacOffFactory = new TeamCityFakeFactory();
        rbacOffFactory.Handler.OnGet("/app/rest/projects", rawProjectsJson);
        var withRbacOff = await CallRbacOffToolAsync(rbacOffFactory, TeamCityToolNames.ListProjects, "{}");

        Assert.Equal(withRbacOff.Text, withGlobalGrant.Text);
    }

    // ---- teamcity_get_project_hierarchy ----

    [Fact]
    public async Task GetProjectHierarchy_ReRootsAVisibleChildUnderAnInvisibleParent_RatherThanDroppingIt()
    {
        const string hiddenParentId = "HiddenParent";
        const string visibleChildId = "VisibleChild";

        var (isError, text) = await RunAsync(TeamCityToolNames.GetProjectHierarchy, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, visibleChildId)]);
            f.Handler.OnGet("/app/rest/projects", $$"""
                {"count":2,"project":[
                    {"id":"{{hiddenParentId}}","name":"Hidden Parent","parentProjectId":"_Root"},
                    {"id":"{{visibleChildId}}","name":"Visible Child","parentProjectId":"{{hiddenParentId}}"}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.Contains("Visible Child", text, StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenParentId, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden Parent", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProjectHierarchy_InvisibleStartId_RendersTheToolsOwnNotFoundString_NotDeniedMessage()
    {
        const string hiddenProjectId = "SomeProject";

        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetProjectHierarchy, $$"""{"projectId":"{{hiddenProjectId}}"}""", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
            f.Handler.OnGet("/app/rest/projects", $$"""
                {"count":1,"project":[{"id":"{{hiddenProjectId}}","name":"Hidden","parentProjectId":"_Root"}]}
                """);
        });

        // A tool body's own "ERROR: ..." string is ordinary tool output, not a filter-level denial —
        // isError:true is reserved for RbacIdentityFilter.DeniedResult(), which never runs here since
        // GetProjectHierarchy is VisibleSetFiltered (no per-call gate check to deny at).
        Assert.False(isError);
        Assert.Equal($"ERROR: Project with ID '{hiddenProjectId}' was not found.", text);
        Assert.NotEqual(ToolGate.DeniedMessage, text);
    }

    // ---- teamcity_search_builds ----

    [Fact]
    public async Task SearchBuilds_FiltersOutBuildsInAnInvisibleProject()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.SearchBuilds, """{"status":"FAILURE"}""", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/builds", $$$"""
                {"count":2,"build":[
                    {"id":1,"number":"1","status":"FAILURE","buildType":{"id":"BtVisible","projectId":"{{{VisibleProject}}}","name":"Visible BT","projectName":"Visible"}},
                    {"id":2,"number":"2","status":"FAILURE","buildType":{"id":"BtHidden","projectId":"{{{HiddenProject}}}","name":"Hidden BT","projectName":"Hidden"}}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.Contains("| 1 |", text, StringComparison.Ordinal);
        Assert.DoesNotContain("| 2 |", text, StringComparison.Ordinal);
        Assert.Contains("**Count:** 1", text, StringComparison.Ordinal);
    }

    // ---- teamcity_get_test_history ----

    [Fact]
    public async Task GetTestHistory_FiltersOutOccurrencesInAnInvisibleProject()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetTestHistory, """{"testName":"MyTest"}""", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/testOccurrences", $$$$"""
                {"count":2,"testOccurrence":[
                    {"status":"FAILURE","duration":100,"build":{"id":1,"number":"1","buildType":{"id":"BtVisible","projectId":"{{{{VisibleProject}}}}","name":"Visible BT"}}},
                    {"status":"FAILURE","duration":200,"build":{"id":2,"number":"2","buildType":{"id":"BtHidden","projectId":"{{{{HiddenProject}}}}","name":"Hidden BT"}}}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.Contains("| 1 |", text, StringComparison.Ordinal);
        Assert.DoesNotContain("| 2 |", text, StringComparison.Ordinal);
        Assert.Contains("**Count:** 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTestHistory_FilteredToEmpty_IsByteIdenticalToGenuinelyEmpty()
    {
        Task<(bool IsError, string Text)> RunWithBuild(string projectId) => RunAsync(
            TeamCityToolNames.GetTestHistory, """{"testName":"MyTest"}""", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/testOccurrences", $$$$"""
                {"count":1,"testOccurrence":[
                    {"status":"FAILURE","duration":100,"build":{"id":1,"number":"1","buildType":{"id":"Bt","projectId":"{{{{projectId}}}}","name":"BT"}}}
                ]}
                """);
        });

        var (_, filteredToEmpty) = await RunWithBuild(HiddenProject);

        var (_, genuinelyEmpty) = await RunAsync(
            TeamCityToolNames.GetTestHistory, """{"testName":"MyTest"}""", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/testOccurrences", """{"count":0,"testOccurrence":[]}""");
        });

        Assert.Equal(genuinelyEmpty, filteredToEmpty);
    }

    // ---- teamcity_list_mutes ----

    [Fact]
    public async Task ListMutes_ProjectScopedMute_IsFilteredOutWhenTheProjectIsHidden()
    {
        var (isError, text) = await RunAsync(TeamCityToolNames.ListMutes, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/mutes", $$$$"""
                {"count":2,"mute":[
                    {"id":1,"scope":{"project":{"id":"{{{{VisibleProject}}}}","name":"Visible"}}},
                    {"id":2,"scope":{"project":{"id":"{{{{HiddenProject}}}}","name":"Hidden"}}}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.Contains("Mute #1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mute #2", text, StringComparison.Ordinal);
        Assert.Contains("**Matched:** 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**Fetched:**", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListMutes_MultiBuildTypeScopedMute_RequiresEveryScopedProjectVisible_NotAny()
    {
        var (isError, text) = await RunAsync(TeamCityToolNames.ListMutes, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
            f.Handler.OnGet("/app/rest/mutes", $$$$"""
                {"count":1,"mute":[
                    {"id":1,"scope":{"buildTypes":{"buildType":[
                        {"id":"BtVisible","projectId":"{{{{VisibleProject}}}}","name":"Visible BT"},
                        {"id":"BtHidden","projectId":"{{{{HiddenProject}}}}","name":"Hidden BT"}
                    ]}}}
                ]}
                """);
        });

        Assert.False(isError);
        Assert.DoesNotContain("Mute #1", text, StringComparison.Ordinal);
        Assert.Contains("**Matched:** 0", text, StringComparison.Ordinal);
    }

    // ---- teamcity_get_audit_log ----

    [Fact]
    public async Task GetAuditLog_Denies_WithoutTheGlobalPermission()
    {
        var (isError, text) = await RunAsync(TeamCityToolNames.GetAuditLog, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", []);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
    }

    [Fact]
    public async Task GetAuditLog_Allows_WithTheGlobalPermission_EvenWhenAffectedProjectIdIsSupplied()
    {
        var (isError, text) = await RunAsync(
            TeamCityToolNames.GetAuditLog, """{"affectedProjectId":"AnyProject"}""", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewAuditLog, null)]);
            f.Handler.OnGet("/app/rest/audit", """{"count":0,"auditEvent":[]}""");
        });

        Assert.False(isError);
    }

    [Fact]
    public async Task GetAuditLog_ProjectScopedGrantOnly_StillDenies_GlobalIsRequired()
    {
        // A project-scoped grant of view_audit_log must not satisfy the global check — the audit
        // log can surface data spanning every project regardless of a scoping argument.
        var (isError, text) = await RunAsync(TeamCityToolNames.GetAuditLog, "{}", f =>
        {
            f.Handler.OnUsers("email", Email, TeamCityUserId);
            f.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewAuditLog, VisibleProject)]);
        });

        Assert.True(isError);
        Assert.Equal(ToolGate.DeniedMessage, text);
    }

    // ---- FilteredOutCount reaches the audit record ----

    [Fact]
    public async Task FilteringSomeRowsOut_ReportsTheDroppedCountInTheAccessAuditRecord()
    {
        using var capture = new LogCapture();
        using var factory = new TeamCityFakeFactory();
        using var mcpAuth = new McpAuthTestConfigBuilder();
        mcpAuth.Apply(factory);
        factory.WithRbacEnabled();
        factory.Handler.OnUsers("email", Email, TeamCityUserId);
        factory.Handler.OnPermissions($"id:{TeamCityUserId}", [(TeamCityPermission.ViewProject, VisibleProject)]);
        factory.Handler.OnGet("/app/rest/projects", $$"""
            {"count":2,"project":[
                {"id":"{{VisibleProject}}","name":"Visible","parentProjectId":"_Root"},
                {"id":"{{HiddenProject}}","name":"Hidden","parentProjectId":"_Root"}
            ]}
            """);

        await CallToolAsync(factory, mcpAuth, TeamCityToolNames.ListProjects, "{}");

        Assert.Contains(capture.Lines, l => l.Contains("filteredOutCount=1", StringComparison.Ordinal));
    }

    private static async Task<(bool IsError, string Text)> RunAsync(
        string toolName, string argumentsJson, Action<TeamCityFakeFactory> configure)
    {
        using var factory = new TeamCityFakeFactory();
        using var mcpAuth = new McpAuthTestConfigBuilder();
        mcpAuth.Apply(factory);
        factory.WithRbacEnabled(auditOnly: false);
        configure(factory);

        return await CallToolAsync(factory, mcpAuth, toolName, argumentsJson);
    }

    private static async Task<(bool IsError, string Text)> CallToolAsync(
        TeamCityFakeFactory factory, McpAuthTestConfigBuilder mcpAuth, string toolName, string argumentsJson)
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

    private static async Task<(bool IsError, string Text)> CallRbacOffToolAsync(
        TeamCityFakeFactory factory, string toolName, string argumentsJson)
    {
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
