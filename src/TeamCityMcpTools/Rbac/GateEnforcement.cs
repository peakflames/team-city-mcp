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
}
