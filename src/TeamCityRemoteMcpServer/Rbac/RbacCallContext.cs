namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// What <see cref="RbacIdentityFilter"/> resolves once per <c>tools/call</c> and makes available to
/// the rest of the request via <see cref="IRbacCallContextAccessor"/>. A new instance is set per
/// call, never reused across calls, so a reference captured mid-call can't observe a later call's
/// data racing in. Every property but <see cref="FilteredOutCount"/> is <c>init</c>-only for exactly
/// that reason; <see cref="FilteredOutCount"/> is the one deliberate exception — see its own doc
/// comment.
/// </summary>
public sealed class RbacCallContext
{
    public required string ToolName { get; init; }

    public required ResourceKind ResourceKind { get; init; }

    /// <summary>Null only for <see cref="ResourceKind.Ungated"/> tools.</summary>
    public string? Permission { get; init; }

    /// <summary>The resource id extracted from the call's arguments (projectId/buildTypeId/buildId/
    /// vcsRootId), by <see cref="ResourceKind"/> convention. Null for <see cref="ResourceKind.CrossProject"/>,
    /// <see cref="ResourceKind.Global"/>, and <see cref="ResourceKind.Ungated"/> tools, or when an
    /// optional resource argument was omitted.</summary>
    public string? Resource { get; init; }

    /// <summary>Raw value of the configured <c>Rbac:IdentityClaim</c> from the caller's JWT. Null
    /// when the claim is absent.</summary>
    public string? IdentityClaimValue { get; init; }

    /// <summary>The TeamCity user id <see cref="IIdentityResolver"/> resolved
    /// <see cref="IdentityClaimValue"/> to. Null when resolution failed or was never attempted.</summary>
    public string? TeamCityUserId { get; init; }

    /// <summary>Set (not init), by design: <see cref="RbacIdentityFilter"/> creates this instance and
    /// hands the accompanying <c>tools/call</c> off to <c>next(...)</c> before this value is known —
    /// a G4 tool body reports it mid-call via <c>IRbacToolCallContext.ReportFilteredOut</c>, and the
    /// filter reads it back afterward, in the same <c>finally</c> that now writes the access audit
    /// record. Safe because the <c>AsyncLocal</c> holds one instance for the lifetime of one call —
    /// no other call's data can land here.</summary>
    public int? FilteredOutCount { get; set; }
}
