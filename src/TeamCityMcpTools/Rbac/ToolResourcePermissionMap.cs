namespace TeamCityMcpTools.Rbac;

/// <summary>
/// The reviewable source of truth for every tool's resource kind and required permission —
/// groups G1-G6 from the RBAC design doc. Hand-written, not built by reflecting over
/// <c>[McpServerTool]</c> attributes: the remote server publishes trimmed
/// (<c>PublishTrimmed</c>, <c>SuppressTrimAnalysisWarnings=true</c>), so a reflection-built map
/// would build clean and fail (or silently miss entries) in the trimmed container. Reflection is
/// fine in the completeness *test*, never here.
///
/// Tool bodies never name a permission directly — they pass a <see cref="TeamCityToolNames"/>
/// const to <see cref="ToolGate"/>, which looks the permission up here. That removes 32 chances to
/// pass the wrong permission and keeps this map the single place a reviewer checks for coverage.
/// </summary>
public static class ToolResourcePermissionMap
{
    public static readonly FrozenDictionary<string, ToolGateSpec> Entries = BuildMap();

    public static bool TryGet(string toolName, out ToolGateSpec spec) => Entries.TryGetValue(toolName, out spec);

    private static FrozenDictionary<string, ToolGateSpec> BuildMap()
    {
        var map = new Dictionary<string, ToolGateSpec>(StringComparer.Ordinal)
        {
            // G1 - project-scoped, projectId is a direct (or optional-but-default) argument (7).
            [TeamCityToolNames.GetProject] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetProjectParameters] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetProjectFeatures] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            // Dual G1/G4: optional projectId. Kind reflects the G1 (projectId given) case; the
            // optional/unscoped case is a ToolGate.BeginForOptionalProjectAsync runtime concern,
            // not something this static map branches on.
            [TeamCityToolNames.ListBuildTypes] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            [TeamCityToolNames.ListTemplates] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            [TeamCityToolNames.ListVcsRoots] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            // vcsRootId resource that pivots to a project; already pivot-free (fields= already
            // requests project(id,name)).
            [TeamCityToolNames.GetVcsRoot] = new(ResourceKind.VcsRoot, TeamCityPermission.ViewProject),

            // G2 - buildType-scoped (4). Needs a buildTypeId -> projectId pivot.
            [TeamCityToolNames.GetBuildType] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildTypeParameters] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildTypeFeatures] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject),
            [TeamCityToolNames.ListBuilds] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject),

            // G3 - build-scoped (10). Needs a buildId -> buildTypeId -> projectId pivot.
            [TeamCityToolNames.GetBuild] = new(ResourceKind.Build, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildStatus] = new(ResourceKind.Build, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildParameters] = new(ResourceKind.Build, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildProblems] = new(ResourceKind.Build, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildChanges] = new(ResourceKind.Build, TeamCityPermission.ViewProject),
            // Also G5: composite/chain-parts fan-out — chain parts are usually in the root build's
            // project, but a part resolving to a different project needs its own check.
            [TeamCityToolNames.GetBuildTests] = new(ResourceKind.Build, TeamCityPermission.ViewProject, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildLogFailures] = new(ResourceKind.Build, TeamCityPermission.ViewBuildRuntimeData),
            [TeamCityToolNames.SearchBuildLog] = new(ResourceKind.Build, TeamCityPermission.ViewBuildRuntimeData),
            [TeamCityToolNames.ListBuildArtifacts] = new(ResourceKind.Build, TeamCityPermission.ViewProject),
            // Highest-priority tool to gate — returns raw artifact bytes.
            [TeamCityToolNames.GetBuildArtifactContent] = new(ResourceKind.Build, TeamCityPermission.ViewFileContent),

            // G4 - cross-project list/search (8). Bulk visible-set + client-side intersection.
            [TeamCityToolNames.ListProjects] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetProjectHierarchy] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject),
            [TeamCityToolNames.SearchBuilds] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject),
            // Dual G1/G4: optional projectId, same convention as the ListBuildTypes group above.
            [TeamCityToolNames.GetRunningBuilds] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetQueuedBuilds] = new(ResourceKind.Project, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetTestHistory] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject),
            [TeamCityToolNames.ListMutes] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject),
            // Flagged in the design doc: surfaces usernames + config-change details server-wide;
            // gated on a global permission, not per-project filtering.
            [TeamCityToolNames.GetAuditLog] = new(ResourceKind.Global, TeamCityPermission.ViewAuditLog),

            // G5 - fan-out beyond teamcity_get_build_tests, already counted in G3 above (2).
            [TeamCityToolNames.GetBuildDependencyTree] = new(ResourceKind.Build, TeamCityPermission.ViewProject, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildTypeDependencyGraph] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, TeamCityPermission.ViewProject),

            // G6 - unavoidably server-wide (1). Deliberate exception, not an omission.
            [TeamCityToolNames.ServerInfo] = new(ResourceKind.Ungated, null),
        };

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
