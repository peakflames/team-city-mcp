namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>Thrown by every resolver method for a definitive "we could not resolve this resource to
/// a project" outcome — a pivot 404, a pivot 403/5xx, or a malformed/empty upstream body. Never
/// thrown for a real "id doesn't exist" vs "id exists but caller can't see the project" distinction:
/// <see cref="RbacGateDecider"/> converts every <see cref="Reason"/> to the same
/// <c>ToolGate.DeniedMessage</c>, so a pivot 404 and a pivot-resolves-to-a-forbidden-project are
/// indistinguishable to the caller — the pivot itself must never become a resource-existence
/// oracle.</summary>
internal sealed class ResourcePivotException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

/// <summary>
/// Resolves a buildType/build/vcsRoot id to the project id that owns it, so
/// <see cref="RbacGateDecider"/> can run the same <see cref="IPermissionGate.CheckProjectAsync"/> check
/// for G2/G3 tools that G1 tools already get from a direct <c>projectId</c> argument. Every method
/// throws <see cref="ResourcePivotException"/> on any failure — there is no "unresolved, allow anyway"
/// path.
/// </summary>
public interface IResourceProjectResolver
{
    ValueTask<string> ResolveProjectForBuildTypeAsync(string buildTypeId, CancellationToken cancellationToken = default);

    ValueTask<string> ResolveProjectForBuildAsync(string buildId, CancellationToken cancellationToken = default);

    ValueTask<string> ResolveProjectForVcsRootAsync(string vcsRootId, CancellationToken cancellationToken = default);
}
