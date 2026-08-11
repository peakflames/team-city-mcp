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

            // vcsRootId resource that pivots to a project. The tool body's own fields= is already
            // pivot-free (requests project(id,name)) — that is a fetch-side optimization, distinct
            // from the enforcement point's pivot, which Session 3 generalizes. Deferred this session.
            [TeamCityToolNames.GetVcsRoot] = new(ResourceKind.VcsRoot, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),

            // G2 - buildType-scoped (4). Needs a buildTypeId -> projectId pivot. Deferred to Session 3.
            [TeamCityToolNames.GetBuildType] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetBuildTypeParameters] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetBuildTypeFeatures] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.ListBuilds] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),

            // G3 - build-scoped (10). Needs a buildId -> buildTypeId -> projectId pivot. Deferred to
            // Session 3.
            [TeamCityToolNames.GetBuild] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetBuildStatus] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetBuildParameters] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetBuildProblems] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetBuildChanges] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            // Also G5: composite/chain-parts fan-out — chain parts are usually in the root build's
            // project, but a part resolving to a different project needs its own check.
            [TeamCityToolNames.GetBuildTests] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildLogFailures] = new(ResourceKind.Build, TeamCityPermission.ViewBuildRuntimeData, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.SearchBuildLog] = new(ResourceKind.Build, TeamCityPermission.ViewBuildRuntimeData, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.ListBuildArtifacts] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            // Highest-priority tool to gate — returns raw artifact bytes.
            [TeamCityToolNames.GetBuildArtifactContent] = new(ResourceKind.Build, TeamCityPermission.ViewFileContent, GateEnforcement.DeferredToLaterSession),

            // G4 - cross-project list/search (5, excluding the dual G1/G4 pair above). Bulk
            // visible-set + client-side intersection — deferred to Session 4.
            [TeamCityToolNames.ListProjects] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetProjectHierarchy] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.SearchBuilds] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.GetTestHistory] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            [TeamCityToolNames.ListMutes] = new(ResourceKind.CrossProject, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession),
            // Flagged in the design doc: surfaces usernames + config-change details server-wide;
            // gated on a global permission, not per-project filtering. Deliberately NOT enforced
            // this session — CheckGlobalAsync's global:true path is untested and the escalation
            // risk (see the session tracker) is real. Lands with Session 4.
            [TeamCityToolNames.GetAuditLog] = new(ResourceKind.Global, TeamCityPermission.ViewAuditLog, GateEnforcement.DeferredToLaterSession),

            // G5 - fan-out beyond teamcity_get_build_tests, already counted in G3 above (2). Deferred.
            [TeamCityToolNames.GetBuildDependencyTree] = new(ResourceKind.Build, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession, TeamCityPermission.ViewProject),
            [TeamCityToolNames.GetBuildTypeDependencyGraph] = new(ResourceKind.BuildType, TeamCityPermission.ViewProject, GateEnforcement.DeferredToLaterSession, TeamCityPermission.ViewProject),

            // G6 - unavoidably server-wide (1). Deliberate exception, not an omission.
            [TeamCityToolNames.ServerInfo] = new(ResourceKind.Ungated, null, GateEnforcement.NeverGated),
        };

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
