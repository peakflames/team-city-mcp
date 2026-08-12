namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Every check throws. Used as the strongest available guard against the G2/G3 landmine
/// regressing: every <see cref="GateEnforcement.DeferredToLaterSession"/> tool must allow without
/// ever reaching a gate call, so wiring one up against a gate that blows up the moment it's touched
/// proves the deferred branch really never calls it.
/// </summary>
public sealed class ThrowingGate : IPermissionGate
{
    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ThrowingGate.CheckProjectAsync should never be called.");

    public ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ThrowingGate.CheckProjectsAsync should never be called.");

    public ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ThrowingGate.CheckGlobalAsync should never be called.");

    public ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ThrowingGate.GetVisibleProjectSetAsync should never be called.");

    public ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ThrowingGate.FilterProjectsAsync should never be called.");
}
