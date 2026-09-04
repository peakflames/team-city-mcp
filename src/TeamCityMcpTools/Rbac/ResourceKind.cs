namespace TeamCityMcpTools.Rbac;

/// <summary>
/// What kind of resource a tool's permission check resolves against. Drives which
/// <see cref="ToolGate"/> Begin*Async overload a gated tool body calls in later sessions —
/// this session, the map exists but nothing calls it from a tool body yet.
/// </summary>
public enum ResourceKind
{
    /// <summary>A direct <c>projectId</c> argument, or one an optional argument resolves to.</summary>
    Project,

    /// <summary>A <c>buildTypeId</c> argument that pivots to a project.</summary>
    BuildType,

    /// <summary>A <c>buildId</c> argument that pivots to a build type, then a project.</summary>
    Build,

    /// <summary>A <c>vcsRootId</c> argument that pivots to a project.</summary>
    VcsRoot,

    /// <summary>No single resource named by the caller — the result set is filtered against the
    /// caller's visible-project set after the fetch (G4), or fanned out to per-node checks (G5).</summary>
    CrossProject,

    /// <summary>Gated on a single server-wide permission rather than any project (e.g. the audit
    /// log), not on project-hierarchy inheritance.</summary>
    Global,

    /// <summary>Deliberate exception — present in the map for completeness-test coverage, but never
    /// checked. See <see cref="TeamCityToolNames.ServerInfo"/>.</summary>
    Ungated,
}
