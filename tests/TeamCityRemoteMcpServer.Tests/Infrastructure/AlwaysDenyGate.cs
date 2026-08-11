namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Promoted out of <c>ToolGateTests</c>' private nested class so
/// <c>RbacGateDeciderTests</c> can share it — a gate that unconditionally denies every check.
/// </summary>
public sealed class AlwaysDenyGate : IPermissionGate
{
    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Deny());

    public ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Deny());

    public ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Deny());

    public ValueTask<IReadOnlyCollection<string>> GetVisibleProjectsAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyCollection<string>>([]);

    public ValueTask<IReadOnlyCollection<T>> FilterAllowedProjectsAsync<T>(
        string toolName,
        string identity,
        IReadOnlyCollection<T> items,
        Func<T, string?> projectIdSelector,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyCollection<T>>([]);
}
