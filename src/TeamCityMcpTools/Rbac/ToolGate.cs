namespace TeamCityMcpTools.Rbac;

/// <summary>
/// Replaces the 5-line <c>CreateAsyncScope()</c> -&gt; <c>ITeamCityClientFactory</c> ->
/// <c>CreateClientAsync()</c> preamble every tool body repeats today, adding a permission check in
/// front of it. Client-creation failure surfaces as <c>Result.Fail(message)</c> so callers keep
/// their existing <c>return $"ERROR: {clientResult.Errors.First().Message}"</c> line unchanged. A
/// denied check surfaces as <see cref="DeniedMessage"/> — byte-identical to a not-found error, per
/// the locked "no resource-existence oracle" decision.
///
/// No tool body calls this yet (Session 1 ships plumbing only) — the four Begin*Async shapes below
/// exist so a future gated tool body's choice of overload documents its own resource kind.
/// </summary>
public static class ToolGate
{
    public const string DeniedMessage = "ERROR: The requested resource was not found.";

    /// <summary>G1 — projectId is a direct, required argument.</summary>
    public static Task<Result<ToolGateSession>> BeginForProjectAsync(
        IServiceProvider serviceProvider,
        IPermissionGate gate,
        string toolName,
        string identity,
        string projectId,
        CancellationToken cancellationToken = default) =>
        BeginCoreAsync(serviceProvider, gate.Enabled
            ? () => gate.CheckProjectAsync(toolName, identity, projectId, cancellationToken)
            : AlwaysAllow);

    /// <summary>Dual G1/G4 — projectId is optional; when omitted, there is no single project to
    /// pre-check and the caller is expected to intersect the fetched result set against
    /// <see cref="IPermissionGate.GetVisibleProjectSetAsync"/> instead.</summary>
    public static Task<Result<ToolGateSession>> BeginForOptionalProjectAsync(
        IServiceProvider serviceProvider,
        IPermissionGate gate,
        string toolName,
        string identity,
        string? projectId,
        CancellationToken cancellationToken = default) =>
        BeginCoreAsync(serviceProvider, gate.Enabled && projectId is not null
            ? () => gate.CheckProjectAsync(toolName, identity, projectId, cancellationToken)
            : AlwaysAllow);

    /// <summary>G4 — no resource named by the caller at all; the session opens unchecked and the
    /// tool body is expected to call <see cref="IPermissionGate.GetVisibleProjectSetAsync"/>
    /// itself, then intersect its own result set against the returned <see cref="VisibleProjectSet"/>.
    /// No G4 tool body actually routes through this overload — every tool body, gated or not, still
    /// uses the same raw <c>CreateAsyncScope()</c> preamble this type was meant to replace, so this
    /// documents the intended shape rather than live behavior.</summary>
    public static Task<Result<ToolGateSession>> BeginForVisibleSetAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        BeginCoreAsync(serviceProvider, AlwaysAllow);

    /// <summary>G2/G3/G5 — the resource id is only known after a pivot fetch (buildTypeId/buildId
    /// -> projectId), or the tool is <see cref="ResourceKind.Global"/>/<see cref="ResourceKind.Ungated"/>.
    /// The session opens unchecked; the caller performs its own <c>gate.Check*Async</c> once the
    /// resource id is resolved.</summary>
    public static Task<Result<ToolGateSession>> BeginDeferredAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        BeginCoreAsync(serviceProvider, AlwaysAllow);

    /// <summary>G4 helper: intersects <paramref name="items"/> against the caller's visible-project
    /// set for <paramref name="toolName"/>'s mapped permission, reporting how many rows were dropped
    /// via <see cref="IRbacToolCallContext.ReportFilteredOut"/> so the access audit record can carry
    /// it. A no-op — returns <paramref name="items"/> unfiltered — when RBAC is disabled, the host is
    /// stdio, or there is no caller identity for this call (all three collapse to
    /// <c>CurrentIdentity is null</c>), or the caller's grant is global.
    ///
    /// <paramref name="serviceProvider"/>'s <see cref="IPermissionGate"/> and
    /// <see cref="IRbacToolCallContext"/> registrations are both singletons, so this resolves them
    /// directly rather than opening a new <c>CreateAsyncScope()</c> just to reach them.</summary>
    public static async Task<IReadOnlyList<T>> FilterByVisibleSetAsync<T>(
        IServiceProvider serviceProvider,
        string toolName,
        IReadOnlyList<T> items,
        Func<T, string?> projectIdSelector,
        CancellationToken cancellationToken = default)
    {
        var gate = serviceProvider.GetRequiredService<IPermissionGate>();
        var callContext = serviceProvider.GetRequiredService<IRbacToolCallContext>();

        if (!gate.Enabled || callContext.CurrentIdentity is not { } identity)
            return items;

        var visibleSet = await gate.GetVisibleProjectSetAsync(toolName, identity, cancellationToken);
        if (visibleSet.IsGlobal)
            return items;

        var filtered = items.Where(item => visibleSet.Contains(projectIdSelector(item))).ToList();

        var droppedCount = items.Count - filtered.Count;
        if (droppedCount > 0)
            callContext.ReportFilteredOut(droppedCount);

        return filtered;
    }

    /// <summary>G5 helper: given a fan-out's discovered project ids, returns the granted subset via
    /// <see cref="IPermissionGate.FilterProjectsAsync"/> — or <c>null</c> when there is nothing to
    /// filter (RBAC disabled, the host is stdio, or there is no caller identity for this call, all
    /// three collapsing to <c>CurrentIdentity is null</c> exactly like <see cref="FilterByVisibleSetAsync"/>).
    /// A <c>null</c> return means the caller renders every discovered node unpruned; a non-null
    /// return (even an empty set) means the caller must treat any node whose own project id is
    /// absent or missing from the set as invisible and prune it, along with every edge touching it.
    /// Distinguishing "filtering inactive" (null) from "filtering active, nothing visible" (empty set)
    /// is what keeps a node with an unresolved project id from being silently shown when RBAC is off.
    /// </summary>
    public static async Task<IReadOnlySet<string>?> FilterVisibleProjectIdsAsync(
        IServiceProvider serviceProvider,
        string toolName,
        IReadOnlyCollection<string> projectIds,
        CancellationToken cancellationToken = default)
    {
        var gate = serviceProvider.GetRequiredService<IPermissionGate>();
        var callContext = serviceProvider.GetRequiredService<IRbacToolCallContext>();

        if (!gate.Enabled || callContext.CurrentIdentity is not { } identity)
            return null;

        return await gate.FilterProjectsAsync(toolName, identity, projectIds, cancellationToken);
    }

    private static ValueTask<GateDecision> AlwaysAllow() => ValueTask.FromResult(GateDecision.Allow());

    private static async Task<Result<ToolGateSession>> BeginCoreAsync(
        IServiceProvider serviceProvider, Func<ValueTask<GateDecision>> check)
    {
        var scope = serviceProvider.CreateAsyncScope();

        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
        {
            await scope.DisposeAsync();
            return Result.Fail(clientResult.Errors.First().Message);
        }

        var decision = await check();
        if (!decision.Allowed)
        {
            await scope.DisposeAsync();
            return Result.Fail(DeniedMessage);
        }

        return Result.Ok(new ToolGateSession(scope, clientResult.Value));
    }
}
