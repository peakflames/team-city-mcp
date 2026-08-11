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
    /// pre-check and the caller (in a future session) is expected to intersect the fetched result
    /// set against <see cref="IPermissionGate.GetVisibleProjectsAsync"/> instead.</summary>
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
    /// tool body is expected to call <see cref="IPermissionGate.FilterAllowedProjectsAsync{T}"/>
    /// itself once it has a result set to intersect.</summary>
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
