namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// Deliberately `sealed class`, never `record` — same rationale as <c>McpAuthOptions</c>: a
/// record's generated ToString() would print every property, and while nothing here is secret
/// today, this section sits next to config that is.
///
/// <c>FailClosed</c> is intentionally not a property here: it is always true and is not
/// configurable. Unresolved identity, a TeamCity 404, or an upstream error all deny once real
/// enforcement lands (Session 2) — there is no knob to relax that.
/// </summary>
public sealed class RbacOptions
{
    public const string SectionName = "Rbac";

    public bool Enabled { get; set; }

    /// <summary>The rollout lever: when true, every check still runs and is still audited, but a
    /// DENY decision never blocks the call. Lets Session 2's real gate ship dark before it is
    /// trusted to actually deny anyone.</summary>
    public bool AuditOnly { get; set; }

    /// <summary>Which JWT claim resolves to a TeamCity identity (by default, via the
    /// <c>email==username</c> convention — see <see cref="TeamCityIdentityResolver"/>).</summary>
    public string IdentityClaim { get; set; } = "email";

    /// <summary>Where the identity value comes from. <see cref="Rbac.IdentitySource.Claim"/> reads
    /// <see cref="IdentityClaim"/> off the validated access token, which is the only option that
    /// works with an authorization server able to put the claim there.
    /// <see cref="Rbac.IdentitySource.UserInfo"/> calls the authorization server's OIDC
    /// <c>/userinfo</c> endpoint with the caller's own token instead — required for an Okta *org*
    /// authorization server, whose access tokens carry no <c>email</c> claim at all. Without this,
    /// identity resolution returns null for every caller and the fail-closed gate denies every call
    /// while the server still reports healthy.</summary>
    public IdentitySource IdentitySource { get; set; } = IdentitySource.Claim;

    /// <summary>Ceiling on how long a <c>/userinfo</c> response is reused. The effective TTL is
    /// <c>min(remaining access-token lifetime, this)</c> — a cached entry must never outlive the token
    /// it was fetched with, or a revoked token would keep resolving to an identity.</summary>
    public int UserInfoCacheTtlSeconds { get; set; } = 300;

    /// <summary>Cache lands Session 2 — this session, the values are bound and validated but
    /// nothing reads them yet.</summary>
    public int PermissionCacheTtlSeconds { get; set; } = 120;

    public int VisibleSetCacheTtlSeconds { get; set; } = 600;

    public int IdentityCacheTtlSeconds { get; set; } = 300;

    /// <summary>buildType-&gt;project is NOT effectively immutable — live testing found build configs
    /// do move between projects — hence a shorter TTL than <see cref="BuildProjectCacheTtlSeconds"/>.</summary>
    public int BuildTypeProjectCacheTtlSeconds { get; set; } = 900;

    /// <summary>build-&gt;project IS immutable once a build exists (a build never moves to a different
    /// build type), so this can safely outlive <see cref="BuildTypeProjectCacheTtlSeconds"/>.</summary>
    public int BuildProjectCacheTtlSeconds { get; set; } = 3600;

    /// <summary>Saturation cap for every RBAC cache, mirroring <c>McpAuth:MaxPendingAuthorizationCodes</c>'s
    /// role as a deliberate bound rather than an unbounded dictionary.</summary>
    public int MaxCacheEntries { get; set; } = 20000;

    /// <summary>Deliberately separate from <see cref="MaxCacheEntries"/> — a permission-cache entry
    /// is a single <c>bool</c>, but a visible-set entry is thousands of project id strings from a
    /// response that can run ~500 KB. 20,000 of those would be gigabytes, not a bounded cache.</summary>
    public int MaxVisibleSetCacheEntries { get; set; } = 2000;
}
