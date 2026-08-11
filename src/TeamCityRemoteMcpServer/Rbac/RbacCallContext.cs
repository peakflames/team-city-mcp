namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// What <see cref="RbacIdentityFilter"/> resolves once per <c>tools/call</c> and makes available to
/// the rest of the request via <see cref="IRbacCallContextAccessor"/>. Immutable — a new instance
/// is set per call, never mutated in place, so a reference captured mid-call can't observe a later
/// call's data racing in.
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
}
