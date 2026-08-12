namespace TeamCityMcpTools.Rbac;

/// <summary>
/// A tool's enforcement stage, as reviewable map data rather than something
/// <see cref="TeamCityRemoteMcpServer.Rbac.RbacGateDecider"/> infers from <see cref="ResourceKind"/>.
/// <see cref="Unspecified"/> is the default of <c>default(ToolGateSpec)</c> and denies — every real
/// map row must name one of the other four values explicitly.
/// </summary>
public enum GateEnforcement
{
    /// <summary>Fail-closed default. A map miss or a forgotten row both deny.</summary>
    Unspecified = 0,

    /// <summary>projectId is a required argument — absent, null, non-string, or blank all deny.</summary>
    RequiredProjectArgument,

    /// <summary>projectId is an optional argument — absent means allow unfiltered (audited as
    /// pending the visible-set filter); a malformed value still denies.</summary>
    OptionalProjectArgument,

    /// <summary>Allowed and audited as explicitly unenforced this session — no gate-calling branch
    /// runs for these tools yet.</summary>
    DeferredToLaterSession,

    /// <summary>Never gated at all. <c>teamcity_server_info</c> only.</summary>
    NeverGated,

    /// <summary>buildTypeId is a required argument that pivots to a project via
    /// <c>IResourceProjectResolver.ResolveProjectForBuildTypeAsync</c> — absent, null, non-string,
    /// blank, or malformed (not <c>^[A-Za-z0-9_.-]+$</c>) all deny; a pivot failure denies too.</summary>
    RequiredBuildTypeArgument,

    /// <summary>buildId is a required argument that pivots to a project via
    /// <c>IResourceProjectResolver.ResolveProjectForBuildAsync</c> — absent, null, non-string,
    /// blank, or malformed (not <c>^[0-9]+$</c>) all deny; a pivot failure denies too.</summary>
    RequiredBuildArgument,

    /// <summary>vcsRootId is a required argument that pivots to a project via
    /// <c>IResourceProjectResolver.ResolveProjectForVcsRootAsync</c> — absent, null, non-string,
    /// blank, or malformed (not <c>^[A-Za-z0-9_.-]+$</c>) all deny; a pivot failure denies too.</summary>
    RequiredVcsRootArgument,

    /// <summary>No single resource named by the caller at all — the gate itself always allows
    /// (subject to the uniform identity check), and the tool body is responsible for calling
    /// <c>IPermissionGate.GetVisibleProjectSetAsync</c> and intersecting its own result set against
    /// it before rendering. Unlike <see cref="DeferredToLaterSession"/>, this is real enforcement:
    /// the filtering just doesn't happen at the gate, since there is nothing here for the gate to
    /// check against.</summary>
    VisibleSetFiltered,

    /// <summary>Gated on a single server-wide permission via <c>IPermissionGate.CheckGlobalAsync</c>,
    /// never on any project — for a tool whose result can surface data spanning every project
    /// regardless of a scoping argument (e.g. the audit log's <c>affectedProjectId</c>).</summary>
    RequiredGlobalPermission,
}
