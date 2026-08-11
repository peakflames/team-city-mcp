namespace TeamCityMcpTools.Rbac;

/// <summary>
/// The default gate for both hosts. Permanent for the stdio host (no HTTP identity ever reaches
/// it); the remote host's RBAC branch <c>Replace()</c>s this registration when <c>Rbac:Enabled</c>
/// is true. Every member allows — this is what makes <c>Rbac:Enabled=false</c> byte-identical to
/// today's behavior.
/// </summary>
public sealed class NoOpPermissionGate : IPermissionGate
{
    public bool Enabled => false;

    public ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow());

    public ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow());

    public ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow());

    public ValueTask<IReadOnlyCollection<string>> GetVisibleProjectsAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyCollection<string>>([]);

    public ValueTask<IReadOnlyCollection<T>> FilterAllowedProjectsAsync<T>(
        string toolName,
        string identity,
        IReadOnlyCollection<T> items,
        Func<T, string?> projectIdSelector,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(items);
}
