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
            // G1 - project-scoped, projectId is a direct required argument (3). Enforced this
            // session.
            [TeamCityToolNames.GetProject] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.RequiredProjectArgument),
            [TeamCityToolNames.GetProjectParameters] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.RequiredProjectArgument),
            [TeamCityToolNames.GetProjectFeatures] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.RequiredProjectArgument),

            // Dual G1/G4: optional projectId (5). Enforced this session — absent projectId allows
            // unfiltered (pending the visible-set filter), a malformed one still denies.
            [TeamCityToolNames.ListBuildTypes] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.OptionalProjectArgument),
            [TeamCityToolNames.ListTemplates] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.OptionalProjectArgument),
            [TeamCityToolNames.ListVcsRoots] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.OptionalProjectArgument),
            [TeamCityToolNames.GetRunningBuilds] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.OptionalProjectArgument),
            [TeamCityToolNames.GetQueuedBuilds] = new(ResourceKind.Project, TeamCityPermission.ViewProject, GateEnforcement.OptionalProjectArgument),

            // vcsRootId resource that pivots to a project via IResourceProjectResolver. Enforced this
            // session (Session 3).
            [TeamCityToolNames.GetVcsRoot] = new(ResourceKind.VcsRoot, TeamCityPermission.ViewProject, GateEnforcement.RequiredVcsRootArgument),

            // G2 - buildType-scoped (4). buildTypeId -> projectId pivot via IResourceProjectResolver.
            // Enforced this session (Session 3).
            [TeamCityToolNames.GetBuildType] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildTypeArgument),
            [TeamCityToolNames.GetBuildTypeParameters] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildTypeArgument),
            [TeamCityToolNames.GetBuildTypeFeatures] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildTypeArgument),
            [TeamCityToolNames.ListBuilds] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildTypeArgument),

            // G3 - build-scoped (10). buildId -> buildTypeId -> projectId pivot via
            // IResourceProjectResolver. Enforced this session (Session 3).
            [TeamCityToolNames.GetBuild] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument),
            [TeamCityToolNames.GetBuildStatus] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument),
            [TeamCityToolNames.GetBuildParameters] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument),
            [TeamCityToolNames.GetBuildProblems] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument),
            [TeamCityToolNames.GetBuildChanges] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument),
            // Also G5: composite/chain-parts fan-out — chain parts are usually in the root build's
            // project, but a part resolving to a different project is pruned from the chain-parts
            // table via CrossProjectPermission. Enforced this session (Session 5).
            [TeamCityToolNames.GetBuildTests] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildLogFailures] = new(ResourceKind.Build, TeamCityPermission.ViewBuildRuntimeData, GateEnforcement.RequiredBuildArgument),
            [TeamCityToolNames.SearchBuildLog] = new(ResourceKind.Build, TeamCityPermission.ViewBuildRuntimeData, GateEnforcement.RequiredBuildArgument),
            [TeamCityToolNames.ListBuildArtifacts] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument),
            // Highest-priority tool to gate — returns raw artifact bytes.
            [TeamCityToolNames.GetBuildArtifactContent] = new(ResourceKind.Build, TeamCityPermission.ViewFileContent, GateEnforcement.RequiredBuildArgument),

            // G4 - cross-project list/search (5, excluding the dual G1/G4 pair above). Bulk
            // visible-set + client-side intersection. Enforced this session (Session 4).
            [TeamCityToolNames.ListProjects] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.VisibleSetFiltered),
            [TeamCityToolNames.GetProjectHierarchy] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.VisibleSetFiltered),
            [TeamCityToolNames.SearchBuilds] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.VisibleSetFiltered),
            [TeamCityToolNames.GetTestHistory] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.VisibleSetFiltered),
            [TeamCityToolNames.ListMutes] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.VisibleSetFiltered),
            // Flagged in the design doc: surfaces usernames + config-change details server-wide;
            // gated on a global permission, not per-project filtering, for every call including one
            // that supplies affectedProjectId. Enforced this session (Session 4).
            [TeamCityToolNames.GetAuditLog] = new(ResourceKind.Global, TeamCityPermission.ViewAuditLog, GateEnforcement.RequiredGlobalPermission),

            // G5 - fan-out beyond teamcity_get_build_tests, already counted in G3 above (2). Root
            // resource gated via the same S3 pivot; fan-out nodes discovered beyond the root are
            // pruned in the tool body via CrossProjectPermission. Enforced this session (Session 5).
            [TeamCityToolNames.GetBuildDependencyTree] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildArgument, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildTypeDependencyGraph] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.RequiredBuildTypeArgument, TeamCityPermission.ViewProject),

            // G6 - unavoidably server-wide (1). Deliberate exception, not an omission.
            [TeamCityToolNames.ServerInfo] = new(ResourceKind.Ungated, null, GateEnforcement.NeverGated),
        };

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
