namespace TeamCityRemoteMcpServer.Auth;

// Deliberately `sealed class`, never `record` — a record's generated ToString() prints every
// property, so one `Log.Debug("{@Options}", o)` would dump config values that shouldn't be logged.
// This is the cheapest structural defense for the "no secret ever logged" rule.

public sealed class McpAuthOptions
{
    public const string SectionName = "McpAuth";

    public bool Enabled { get; set; }

    /// <summary>The external authorization server's issuer URI, e.g.
    /// https://issuer.okta.example.invalid/oauth2/&lt;asid&gt; (Okta Custom AS) or an internal
    /// shared AS. JwtBearer fetches its OIDC discovery document and JWKS from this Authority.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Explicit override for the discovery document location. Optional escape hatch —
    /// an Okta Custom AS answers on .../oauth2/&lt;asid&gt;/.well-known/openid-configuration
    /// (what .NET's Authority handling appends by default), but its
    /// .../oauth2/&lt;asid&gt;/.well-known/oauth-authorization-server is not in the MCP spec's
    /// client probe list, so it should not be relied on implicitly.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>This server's own resource identifier — both the JWT audience and the RFC 9728
    /// protected-resource-metadata `resource` value, e.g.
    /// https://teamcity-mcp.example.invalid/mcp.</summary>
    public string ResourceUri { get; set; } = string.Empty;

    public List<string> ScopesSupported { get; set; } = [OAuthScopes.Read];

    /// <summary>
    /// Whether to advertise <see cref="ScopesSupported"/> in the RFC 9728 protected-resource
    /// metadata at all. Set false for an authorization server with no custom-scope capability:
    /// advertising <c>teamcity:read</c> there makes a conforming client request a scope the
    /// authorization server will refuse, failing the whole authorization request.
    ///
    /// This is a separate key rather than "configure an empty list" because an empty list is not
    /// expressible through configuration. <c>ConfigurationBinder</c> *appends* to an existing
    /// <c>List&lt;T&gt;</c>, so <see cref="ScopesSupported"/> can only ever grow beyond its default —
    /// measured, not assumed: binding <c>McpAuth:ScopesSupported:0=zzz</c> yields
    /// <c>[teamcity:read, zzz]</c>.
    /// </summary>
    public bool AdvertiseScopes { get; set; } = true;

    public int ClockSkewSeconds { get; set; } = 30;

    /// <summary>Whether to bind the token to <see cref="ResourceUri"/> via the `aud` claim, as the
    /// MCP spec requires. Set false only for an authorization server that cannot mint a
    /// per-resource audience — an Okta *org* authorization server always stamps `aud` with its own
    /// issuer, so audience binding is unavailable there at any price. Turning this off without an
    /// <see cref="AllowedClientIds"/> entry is refused at startup by McpAuthOptionsValidator.</summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>Allowlist of OAuth client ids permitted to call this server, matched against the
    /// access token's `cid` claim. The documented substitute for audience binding: it does not
    /// prove the token was minted *for* this resource, only that it was minted for a client we
    /// recognize. Empty (the default) means no client check — safe only while
    /// <see cref="ValidateAudience"/> is true, which the startup validator enforces.</summary>
    public List<string> AllowedClientIds { get; set; } = [];

    /// <summary>Whether a caller's token must carry the <see cref="OAuthScopes.Read"/> scope. Set
    /// false for an authorization server with no custom-scope capability, where no scope value can
    /// convey resource-specific authorization and the real authorization decision is made
    /// downstream by the RBAC gate against the upstream system's own permission model.</summary>
    public bool RequireScope { get; set; } = true;
}
