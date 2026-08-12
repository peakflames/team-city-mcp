namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Pure unit coverage for <see cref="RbacGateDecider"/> — no HTTP, no DI, no ASP.NET host. Turns the
/// argument-shape matrix that would otherwise need 20 HTTP round trips into microsecond-fast tests.
/// </summary>
public class RbacGateDeciderTests
{
    private const string RequiredTool = TeamCityToolNames.GetProject;
    private const string OptionalTool = TeamCityToolNames.ListBuildTypes;
    private const string Identity = "42";

    /// <summary>None of this file's cases exercise a BuildType/Build/VcsRoot resource kind, so the
    /// resolver is never reached — every method throws to make that assumption loud if it ever stops
    /// holding.</summary>
    private static readonly IResourceProjectResolver NeverCalledResolver = new NeverCalledResourceProjectResolver();

    [Theory]
    [InlineData("""{"projectId":123}""")]
    [InlineData("""{"projectId":true}""")]
    [InlineData("""{"projectId":[]}""")]
    [InlineData("""{"projectId":{}}""")]
    [InlineData("""{"projectId":""}""")]
    [InlineData("""{"projectId":"   "}""")]
    public async Task RequiredProjectArgument_MalformedShapes_Deny_AndNeverCallTheGate(string argumentsJson)
    {
        var gate = new CountingGate();
        var arguments = ParseArguments(argumentsJson);

        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, arguments, out var resource);
        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, RequiredTool, Identity, state, resource, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("resource_argument_malformed", decision.Reason);
        Assert.Equal(0, gate.ProjectCallCount);
    }

    [Fact]
    public async Task RequiredProjectArgument_WrongCaseKey_DeniesAsMissing_NotAsPresent()
    {
        var gate = new CountingGate();
        var arguments = ParseArguments("""{"ProjectId":"P"}""");

        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, arguments, out var resource);
        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, RequiredTool, Identity, state, resource, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("resource_argument_missing", decision.Reason);
        Assert.Equal(0, gate.ProjectCallCount);
    }

    [Fact]
    public async Task RequiredProjectArgument_ValuePassedVerbatim_NeverTrimmedOrNormalized()
    {
        var gate = new CountingGate();
        var arguments = ParseArguments("""{"projectId":" P "}""");

        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, arguments, out var resource);
        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, RequiredTool, Identity, state, resource, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal(1, gate.ProjectCallCount);
        Assert.Equal(" P ", gate.LastProjectId);
    }

    [Fact]
    public async Task OptionalProjectArgument_Absent_Allows_AndNeverCallsTheGate()
    {
        var gate = new CountingGate();
        var arguments = ParseArguments("{}");

        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, arguments, out var resource);
        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, OptionalTool, Identity, state, resource, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal("unscoped_pending_visible_set", decision.Reason);
        Assert.Equal(0, gate.ProjectCallCount);
    }

    [Fact]
    public async Task OptionalProjectArgument_Malformed_StillDenies_MalformedIsNeverTreatedAsAbsent()
    {
        var gate = new CountingGate();
        var arguments = ParseArguments("""{"projectId":123}""");

        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, arguments, out var resource);
        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, OptionalTool, Identity, state, resource, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("resource_argument_malformed", decision.Reason);
        Assert.Equal(0, gate.ProjectCallCount);
    }

    [Fact]
    public async Task OptionalProjectArgument_Present_ChecksTheGate()
    {
        var gate = new CountingGate();
        var arguments = ParseArguments("""{"projectId":"MyProject"}""");

        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, arguments, out var resource);
        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, OptionalTool, Identity, state, resource, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal(1, gate.ProjectCallCount);
        Assert.Equal("MyProject", gate.LastProjectId);
    }

    public static IEnumerable<object[]> AllGatedToolNames() =>
        ToolResourcePermissionMap.Entries
            .Where(e => e.Value.Enforcement != GateEnforcement.NeverGated)
            .Select(e => new object[] { e.Key });

    [Theory]
    [MemberData(nameof(AllGatedToolNames))]
    public async Task NullIdentity_Denies_ForEveryGatedTool_RegardlessOfEnforcementStage(string toolName)
    {
        var gate = new CountingGate();
        var state = RbacGateDecider.TryExtractResource(ResourceKind.Project, null, out var resource);

        var decision = await RbacGateDecider.DecideAsync(gate, NeverCalledResolver, toolName, identity: null, state, resource, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("identity_unresolved", decision.Reason);
    }

    public static IEnumerable<object[]> VisibleSetFilteredToolNames() =>
        ToolResourcePermissionMap.Entries
            .Where(e => e.Value.Enforcement == GateEnforcement.VisibleSetFiltered)
            .Select(e => new object[] { e.Key });

    [Theory]
    [MemberData(nameof(VisibleSetFilteredToolNames))]
    public async Task VisibleSetFilteredTools_AllowWithNoGateCall_FilteringHappensInTheToolBodyInstead(string toolName)
    {
        var gate = new ThrowingGate();

        var decision = await RbacGateDecider.DecideAsync(
            gate, NeverCalledResolver, toolName, Identity, RbacGateDecider.ArgumentState.Absent, resource: null, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal("visible_set_filtering_applied", decision.Reason);
    }

    [Fact]
    public async Task RequiredGlobalPermission_ChecksTheGate_AndPropagatesItsDecision()
    {
        var gate = new CountingGate();

        var decision = await RbacGateDecider.DecideAsync(
            gate, NeverCalledResolver, TeamCityToolNames.GetAuditLog, Identity, RbacGateDecider.ArgumentState.Absent, resource: null, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal(1, gate.GlobalCallCount);
    }

    [Fact]
    public async Task RequiredGlobalPermission_Denies_WhenTheGateDenies()
    {
        var gate = new AlwaysDenyGate();

        var decision = await RbacGateDecider.DecideAsync(
            gate, NeverCalledResolver, TeamCityToolNames.GetAuditLog, Identity, RbacGateDecider.ArgumentState.Absent, resource: null, CancellationToken.None);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task NeverGatedTool_Allows_EvenAgainstAThrowingGate_AndEvenWithNullIdentity()
    {
        var gate = new ThrowingGate();

        var decision = await RbacGateDecider.DecideAsync(
            gate, NeverCalledResolver, TeamCityToolNames.ServerInfo, identity: null, RbacGateDecider.ArgumentState.Absent, resource: null, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal("never_gated", decision.Reason);
    }

    [Fact]
    public async Task DisabledGate_AllowsEveryTool_WithoutConsultingTheMap()
    {
        var gate = new CountingGate2 { Enabled = false };

        var decision = await RbacGateDecider.DecideAsync(
            gate, NeverCalledResolver, "not_a_real_tool_name", Identity, RbacGateDecider.ArgumentState.Absent, resource: null, CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal("rbac_disabled", decision.Reason);
    }

    [Fact]
    public async Task UnmappedTool_Denies_AsDefenseInDepth()
    {
        var gate = new CountingGate();

        var decision = await RbacGateDecider.DecideAsync(
            gate, NeverCalledResolver, "not_a_real_tool_name", Identity, RbacGateDecider.ArgumentState.Absent, resource: null, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("tool_unmapped", decision.Reason);
    }

    private static IDictionary<string, JsonElement>? ParseArguments(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private sealed class NeverCalledResourceProjectResolver : IResourceProjectResolver
    {
        public ValueTask<string> ResolveProjectForBuildTypeAsync(string buildTypeId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called by this file's Project/deferred/unmapped test cases.");

        public ValueTask<string> ResolveProjectForBuildAsync(string buildId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called by this file's Project/deferred/unmapped test cases.");

        public ValueTask<string> ResolveProjectForVcsRootAsync(string vcsRootId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called by this file's Project/deferred/unmapped test cases.");
    }

    /// <summary>A minimal gate whose <see cref="Enabled"/> can be toggled, for the
    /// <c>!gate.Enabled</c> short-circuit test — distinct from <see cref="CountingGate"/>, whose
    /// <c>Enabled</c> is fixed true.</summary>
    private sealed class CountingGate2 : IPermissionGate
    {
        public bool Enabled { get; init; }

        public ValueTask<GateDecision> CheckProjectAsync(
            string toolName, string identity, string projectId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called when the gate is disabled.");

        public ValueTask<GateDecision> CheckProjectsAsync(
            string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called when the gate is disabled.");

        public ValueTask<GateDecision> CheckGlobalAsync(
            string toolName, string identity, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called when the gate is disabled.");

        public ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
            string toolName, string identity, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called when the gate is disabled.");

        public ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
            string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never be called when the gate is disabled.");
    }
}
