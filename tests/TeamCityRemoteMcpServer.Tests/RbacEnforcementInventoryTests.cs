namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Pins <see cref="ToolResourcePermissionMap"/>'s per-tool <see cref="GateEnforcement"/> membership.
/// Session 3 (the buildType/build/vcsRoot pivot resolver) moved G2 (4), G3 (10), and
/// <c>teamcity_get_vcs_root</c> (1) out of <see cref="GateEnforcement.DeferredToLaterSession"/> into
/// their own Required*Argument stages. Session 4 (visible-set filtering + the audit log's global
/// check) moved G4's 5 cross-project tools to <see cref="GateEnforcement.VisibleSetFiltered"/> and
/// the audit log to <see cref="GateEnforcement.RequiredGlobalPermission"/>. Session 5 (fan-out
/// pruning) moved G5's 2 tools (<c>teamcity_get_build_dependency_tree</c>,
/// <c>teamcity_get_build_type_dependency_graph</c>) into their Required*Argument stages too — their
/// root resource was already gated via the S3 pivot; what changed is fan-out nodes beyond that root
/// now get pruned in the tool body via <c>CrossProjectPermission</c>. 3/5/4/10/1/5/1/0/1 now: zero
/// rows left at <see cref="GateEnforcement.DeferredToLaterSession"/>. Also enforces the hard
/// compile-time guarantee (a required positional <c>Enforcement</c> parameter) that no row is left at
/// <see cref="GateEnforcement.Unspecified"/>.
/// </summary>
public class RbacEnforcementInventoryTests
{
    private static readonly string[] RequiredProjectArgumentTools =
    [
        TeamCityToolNames.GetProject,
        TeamCityToolNames.GetProjectParameters,
        TeamCityToolNames.GetProjectFeatures,
    ];

    private static readonly string[] OptionalProjectArgumentTools =
    [
        TeamCityToolNames.ListBuildTypes,
        TeamCityToolNames.ListTemplates,
        TeamCityToolNames.ListVcsRoots,
        TeamCityToolNames.GetRunningBuilds,
        TeamCityToolNames.GetQueuedBuilds,
    ];

    private static readonly string[] RequiredBuildTypeArgumentTools =
    [
        TeamCityToolNames.GetBuildType,
        TeamCityToolNames.GetBuildTypeParameters,
        TeamCityToolNames.GetBuildTypeFeatures,
        TeamCityToolNames.ListBuilds,
        TeamCityToolNames.GetBuildTypeDependencyGraph,
    ];

    private static readonly string[] RequiredBuildArgumentTools =
    [
        TeamCityToolNames.GetBuild,
        TeamCityToolNames.GetBuildStatus,
        TeamCityToolNames.GetBuildParameters,
        TeamCityToolNames.GetBuildProblems,
        TeamCityToolNames.GetBuildChanges,
        TeamCityToolNames.GetBuildTests,
        TeamCityToolNames.GetBuildLogFailures,
        TeamCityToolNames.SearchBuildLog,
        TeamCityToolNames.ListBuildArtifacts,
        TeamCityToolNames.GetBuildArtifactContent,
        TeamCityToolNames.GetBuildDependencyTree,
    ];

    private static readonly string[] RequiredVcsRootArgumentTools = [TeamCityToolNames.GetVcsRoot];

    private static readonly string[] VisibleSetFilteredTools =
    [
        TeamCityToolNames.ListProjects,
        TeamCityToolNames.GetProjectHierarchy,
        TeamCityToolNames.SearchBuilds,
        TeamCityToolNames.GetTestHistory,
        TeamCityToolNames.ListMutes,
    ];

    private static readonly string[] RequiredGlobalPermissionTools = [TeamCityToolNames.GetAuditLog];

    private static readonly string[] DeferredToLaterSessionTools = [];

    private static readonly string[] NeverGatedTools = [TeamCityToolNames.ServerInfo];

    private static readonly IReadOnlySet<(ResourceKind Kind, GateEnforcement Enforcement)> LegalPairs =
        new HashSet<(ResourceKind, GateEnforcement)>
        {
            (ResourceKind.Project, GateEnforcement.RequiredProjectArgument),
            (ResourceKind.Project, GateEnforcement.OptionalProjectArgument),
            (ResourceKind.VcsRoot, GateEnforcement.RequiredVcsRootArgument),
            (ResourceKind.BuildType, GateEnforcement.RequiredBuildTypeArgument),
            (ResourceKind.Build, GateEnforcement.RequiredBuildArgument),
            (ResourceKind.CrossProject, GateEnforcement.VisibleSetFiltered),
            (ResourceKind.Global, GateEnforcement.RequiredGlobalPermission),
            (ResourceKind.Ungated, GateEnforcement.NeverGated),
        };

    [Fact]
    public void StageMembership_MatchesTheLockedSessionInventory()
    {
        AssertStage(RequiredProjectArgumentTools, GateEnforcement.RequiredProjectArgument);
        AssertStage(OptionalProjectArgumentTools, GateEnforcement.OptionalProjectArgument);
        AssertStage(RequiredBuildTypeArgumentTools, GateEnforcement.RequiredBuildTypeArgument);
        AssertStage(RequiredBuildArgumentTools, GateEnforcement.RequiredBuildArgument);
        AssertStage(RequiredVcsRootArgumentTools, GateEnforcement.RequiredVcsRootArgument);
        AssertStage(VisibleSetFilteredTools, GateEnforcement.VisibleSetFiltered);
        AssertStage(RequiredGlobalPermissionTools, GateEnforcement.RequiredGlobalPermission);
        AssertStage(DeferredToLaterSessionTools, GateEnforcement.DeferredToLaterSession);
        AssertStage(NeverGatedTools, GateEnforcement.NeverGated);

        Assert.Equal(3, RequiredProjectArgumentTools.Length);
        Assert.Equal(5, OptionalProjectArgumentTools.Length);
        Assert.Equal(5, RequiredBuildTypeArgumentTools.Length);
        Assert.Equal(11, RequiredBuildArgumentTools.Length);
        Assert.Single(RequiredVcsRootArgumentTools);
        Assert.Equal(5, VisibleSetFilteredTools.Length);
        Assert.Single(RequiredGlobalPermissionTools);
        Assert.Empty(DeferredToLaterSessionTools);
        Assert.Single(NeverGatedTools);
        Assert.Equal(32, ToolResourcePermissionMap.Entries.Count);
    }

    [Fact]
    public void NoRow_IsLeftAtUnspecified()
    {
        foreach (var (toolName, spec) in ToolResourcePermissionMap.Entries)
        {
            Assert.True(
                spec.Enforcement != GateEnforcement.Unspecified,
                $"{toolName} is at GateEnforcement.Unspecified — every row must state an explicit stage.");
        }
    }

    [Fact]
    public void EveryRow_HasALegalKindEnforcementPair()
    {
        foreach (var (toolName, spec) in ToolResourcePermissionMap.Entries)
        {
            Assert.True(
                LegalPairs.Contains((spec.Kind, spec.Enforcement)),
                $"{toolName} has an unrecognized (Kind={spec.Kind}, Enforcement={spec.Enforcement}) pair.");
        }
    }

    [Fact]
    public void Permission_IsNull_IfAndOnlyIf_NeverGated()
    {
        foreach (var (toolName, spec) in ToolResourcePermissionMap.Entries)
        {
            if (spec.Enforcement == GateEnforcement.NeverGated)
            {
                Assert.True(spec.Permission is null, $"{toolName} is NeverGated but has a non-null Permission.");
            }
            else
            {
                Assert.True(spec.Permission is not null, $"{toolName} is gated but has a null Permission.");
            }
        }
    }

    private static void AssertStage(IReadOnlyCollection<string> tools, GateEnforcement expected)
    {
        foreach (var toolName in tools)
        {
            Assert.True(ToolResourcePermissionMap.TryGet(toolName, out var spec), $"{toolName} has no map entry.");
            Assert.True(
                spec.Enforcement == expected,
                $"{toolName}: expected {expected}, got {spec.Enforcement}.");
        }
    }
}
