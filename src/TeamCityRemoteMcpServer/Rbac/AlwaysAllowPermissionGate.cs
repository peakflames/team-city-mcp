namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// This session's gate: <see cref="Enabled"/> is true (so wiring, identity resolution, and the
/// audit sink are all genuinely exercised end to end against a real deployment), but every check
/// unconditionally allows. Session 2 replaces this with a real
/// <c>TeamCityPermissionGate</c> that actually queries
/// <c>GET /app/rest/users/{locator}/permissions</c> — this class exists so that swap is the only
/// thing that changes; nothing about the filter, the map, or the audit sink needs to move.
/// </summary>
public sealed class AlwaysAllowPermissionGate : IPermissionGate
{
    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow("session-1-unenforced"));

    public ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow("session-1-unenforced"));

    public ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow("session-1-unenforced"));

    /// <summary>Not consulted this session — no tool body calls into visible-set filtering yet, and
    /// this gate has no real permission data to return. Session 2's cache-backed implementation
    /// replaces this.</summary>
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
