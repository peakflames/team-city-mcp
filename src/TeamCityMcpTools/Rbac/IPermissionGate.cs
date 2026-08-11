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

    /// <summary>Full set of project ids the identity holds <c>view_project</c> (or the given
    /// permission) on. Used by G4 tools to intersect against an unfiltered result set.</summary>
    ValueTask<IReadOnlyCollection<string>> GetVisibleProjectsAsync(
        string toolName, string identity, CancellationToken cancellationToken = default);

    /// <summary>Convenience wrapper over <see cref="GetVisibleProjectsAsync"/> for G4 tools:
    /// returns only the items whose project id (via <paramref name="projectIdSelector"/>) is in the
    /// identity's visible set.</summary>
    ValueTask<IReadOnlyCollection<T>> FilterAllowedProjectsAsync<T>(
        string toolName,
        string identity,
        IReadOnlyCollection<T> items,
        Func<T, string?> projectIdSelector,
        CancellationToken cancellationToken = default);
}
