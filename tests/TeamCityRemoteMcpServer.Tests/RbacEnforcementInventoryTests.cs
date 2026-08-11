namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Pins <see cref="ToolResourcePermissionMap"/>'s per-tool <see cref="GateEnforcement"/> membership
/// before any decision logic changes — the 3/5/23/1 split this session locks in, and the hard
/// compile-time guarantee (a required positional <c>Enforcement</c> parameter) that no row is left
/// at <see cref="GateEnforcement.Unspecified"/>.
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

    private static readonly string[] DeferredToLaterSessionTools =
    [
        TeamCityToolNames.GetVcsRoot,
        TeamCityToolNames.GetBuildType,
        TeamCityToolNames.GetBuildTypeParameters,
        TeamCityToolNames.GetBuildTypeFeatures,
        TeamCityToolNames.ListBuilds,
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
        TeamCityToolNames.ListProjects,
        TeamCityToolNames.GetProjectHierarchy,
        TeamCityToolNames.SearchBuilds,
        TeamCityToolNames.GetTestHistory,
        TeamCityToolNames.ListMutes,
        TeamCityToolNames.GetAuditLog,
        TeamCityToolNames.GetBuildDependencyTree,
        TeamCityToolNames.GetBuildTypeDependencyGraph,
    ];

    private static readonly string[] NeverGatedTools = [TeamCityToolNames.ServerInfo];

    private static readonly IReadOnlySet<(ResourceKind Kind, GateEnforcement Enforcement)> LegalPairs =
        new HashSet<(ResourceKind, GateEnforcement)>
        {
            (ResourceKind.Project, GateEnforcement.RequiredProjectArgument),
            (ResourceKind.Project, GateEnforcement.OptionalProjectArgument),
            (ResourceKind.VcsRoot, GateEnforcement.DeferredToLaterSession),
            (ResourceKind.BuildType, GateEnforcement.DeferredToLaterSession),
            (ResourceKind.Build, GateEnforcement.DeferredToLaterSession),
            (ResourceKind.CrossProject, GateEnforcement.DeferredToLaterSession),
            (ResourceKind.Global, GateEnforcement.DeferredToLaterSession),
            (ResourceKind.Ungated, GateEnforcement.NeverGated),
        };

    [Fact]
    public void StageMembership_MatchesTheLockedSessionInventory()
    {
        AssertStage(RequiredProjectArgumentTools, GateEnforcement.RequiredProjectArgument);
        AssertStage(OptionalProjectArgumentTools, GateEnforcement.OptionalProjectArgument);
        AssertStage(DeferredToLaterSessionTools, GateEnforcement.DeferredToLaterSession);
        AssertStage(NeverGatedTools, GateEnforcement.NeverGated);

        Assert.Equal(3, RequiredProjectArgumentTools.Length);
        Assert.Equal(5, OptionalProjectArgumentTools.Length);
        Assert.Equal(23, DeferredToLaterSessionTools.Length);
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
