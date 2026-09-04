namespace TeamCityMcpTools;

/// <summary>
/// The `Name =` string for every <c>[McpServerTool]</c>, used both in the attribute itself and as
/// the key into <see cref="TeamCityMcpTools.Rbac.ToolResourcePermissionMap"/>. Having tool bodies
/// pass these consts (rather than the map looking up tool names reflectively, and rather than tool
/// bodies naming a permission directly) removes 32 chances to typo a tool name out of sync with its
/// map entry — the completeness test cross-checks this class, the map, and the live `tools/list`
/// response against each other.
/// </summary>
public static class TeamCityToolNames
{
    // BuildTools
    public const string GetBuild = "teamcity_get_build";
    public const string GetBuildArtifactContent = "teamcity_get_build_artifact_content";
    public const string GetBuildChanges = "teamcity_get_build_changes";
    public const string GetBuildDependencyTree = "teamcity_get_build_dependency_tree";
    public const string GetBuildLogFailures = "teamcity_get_build_log_failures";
    public const string GetBuildParameters = "teamcity_get_build_parameters";
    public const string GetBuildProblems = "teamcity_get_build_problems";
    public const string GetBuildStatus = "teamcity_get_build_status";
    public const string GetBuildTests = "teamcity_get_build_tests";
    public const string GetQueuedBuilds = "teamcity_get_queued_builds";
    public const string GetRunningBuilds = "teamcity_get_running_builds";
    public const string GetTestHistory = "teamcity_get_test_history";
    public const string ListBuildArtifacts = "teamcity_list_build_artifacts";
    public const string ListMutes = "teamcity_list_mutes";
    public const string ListBuilds = "teamcity_list_builds";
    public const string SearchBuildLog = "teamcity_search_build_log";
    public const string SearchBuilds = "teamcity_search_builds";

    /// <summary>Deliberately ungated (G6) — see <see cref="TeamCityMcpTools.Rbac.ResourceKind.Ungated"/>.
    /// Returns only server version/build number, no project data.</summary>
    public const string ServerInfo = "teamcity_server_info";

    // ProjectTools
    public const string GetAuditLog = "teamcity_get_audit_log";
    public const string GetBuildType = "teamcity_get_build_type";
    public const string GetBuildTypeDependencyGraph = "teamcity_get_build_type_dependency_graph";
    public const string GetBuildTypeFeatures = "teamcity_get_build_type_features";
    public const string GetBuildTypeParameters = "teamcity_get_build_type_parameters";
    public const string GetProject = "teamcity_get_project";
    public const string GetProjectFeatures = "teamcity_get_project_features";
    public const string GetProjectHierarchy = "teamcity_get_project_hierarchy";
    public const string GetProjectParameters = "teamcity_get_project_parameters";
    public const string GetVcsRoot = "teamcity_get_vcs_root";
    public const string ListBuildTypes = "teamcity_list_build_types";
    public const string ListProjects = "teamcity_list_projects";
    public const string ListTemplates = "teamcity_list_templates";
    public const string ListVcsRoots = "teamcity_list_vcs_roots";
}
