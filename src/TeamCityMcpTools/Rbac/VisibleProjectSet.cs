namespace TeamCityMcpTools.Rbac;

/// <summary>
/// The set of project ids an identity holds a given permission on, as returned by
/// <see cref="IPermissionGate.GetVisibleProjectSetAsync"/>. <see cref="IsGlobal"/> is the *absence*
/// of a finite set, not "every known project id" — a server admin's grant is never materialized as
/// a list, so their filtered output stays byte-identical to the RBAC-off output regardless of how
/// many projects exist.
/// </summary>
public sealed class VisibleProjectSet
{
    private static readonly VisibleProjectSet GlobalInstance = new(isGlobal: true, projectIds: null);

    private readonly IReadOnlySet<string>? _projectIds;

    private VisibleProjectSet(bool isGlobal, IReadOnlySet<string>? projectIds)
    {
        IsGlobal = isGlobal;
        _projectIds = projectIds;
    }

    /// <summary>True when the identity holds the permission via a project-less (global) grant —
    /// visible everywhere, including projects created after this was computed.</summary>
    public bool IsGlobal { get; }

    public static VisibleProjectSet Global() => GlobalInstance;

    public static VisibleProjectSet Scoped(IReadOnlySet<string> projectIds) => new(isGlobal: false, projectIds);

    /// <summary>True if <paramref name="projectId"/> is visible — always true when
    /// <see cref="IsGlobal"/>, otherwise a membership check against the finite set. A null id is
    /// never visible, even under a global grant: a caller with no project id to check has nothing
    /// this method can answer for.</summary>
    public bool Contains(string? projectId) =>
        IsGlobal || (projectId is not null && _projectIds!.Contains(projectId));
}
