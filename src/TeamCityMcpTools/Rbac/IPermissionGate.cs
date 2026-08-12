namespace TeamCityMcpTools.Rbac;

/// <summary>
/// Asks TeamCity's own already-computed permission decision (via
/// <c>GET /app/rest/users/{locator}/permissions</c>) whether a resolved identity holds a given
/// permission on a resource. Never re-implements TeamCity's permission model — every method is a
/// pass-through query. Resolved from <c>scope.ServiceProvider</c> exactly like
/// <see cref="ITeamCityClientFactory"/>: no <c>ClaimsPrincipal</c>, no ASP.NET types, no audit
/// concepts here — those live in the remote host, which resolves a caller down to a plain identity
/// string before ever calling into this interface.
/// </summary>
public interface IPermissionGate
{
    /// <summary>False for <see cref="NoOpPermissionGate"/> and whenever <c>Rbac:Enabled</c> is
    /// false — callers should skip calling the other members entirely rather than rely on them to
    /// no-op, since a disabled gate has nothing meaningful to say about visibility.</summary>
    bool Enabled { get; }

    ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default);

    ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default);

    ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default);

    /// <summary>The set of project ids the identity holds <paramref name="toolName"/>'s mapped
    /// permission on, as a <see cref="VisibleProjectSet"/> — used by G4 tools to intersect an
    /// unfiltered result set against the caller's actual visibility. Unlike a bare
    /// <c>IReadOnlyCollection&lt;string&gt;</c>, this can represent a global grant without
    /// materializing every project id that exists.</summary>
    ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
        string toolName, string identity, CancellationToken cancellationToken = default);

    /// <summary>The subset of <paramref name="projectIds"/> the identity holds <paramref name="toolName"/>'s
    /// mapped permission on (<see cref="ToolGateSpec.CrossProjectPermission"/> when set, since a G5
    /// fan-out node discovered outside the tool's own named resource is checked against that
    /// permission, not necessarily the root resource's). Distinct from <see cref="CheckProjectsAsync"/>,
    /// which is deliberately all-or-nothing — this returns exactly the granted ids so a caller can
    /// prune the rest. Given a known, already-bounded id set (a fan-out's discovered nodes), never
    /// materializes "every project that exists" the way a <see cref="VisibleProjectSet"/> global grant
    /// would.</summary>
    ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default);
}
