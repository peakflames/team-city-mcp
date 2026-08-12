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

    // Global(), not a Scoped empty set: Enabled=false means never filter, matching every other
    // member here — a caller intersecting a result set against this must see everything through.
    public ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(VisibleProjectSet.Global());

    // Every id passed in comes back granted, matching every other member here — but callers should
    // never actually reach this: ToolGate.FilterVisibleProjectIdsAsync short-circuits on !Enabled
    // before calling it, the same guard every other Enabled-gated helper in this file relies on.
    public ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlySet<string>>(new HashSet<string>(projectIds, StringComparer.Ordinal));
}
