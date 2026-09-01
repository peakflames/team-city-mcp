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

    /// <summary>Scopes advertised in the RFC 9728 protected-resource metadata, and therefore the
    /// scopes a conforming client goes on to request. Configuring this key *replaces* the default
    /// rather than appending to it — see <see cref="ReplaceConfiguredScopesSupported"/>, which
    /// exists solely to defeat <c>ConfigurationBinder</c>'s append-to-existing-collection
    /// behavior. An org-authorization-server deployment sets this to the OIDC scopes the tenant can
    /// actually grant, e.g. <c>openid email profile offline_access</c>.</summary>
    public List<string> ScopesSupported { get; set; } = [OAuthScopes.Read];

    /// <summary>
    /// Whether to advertise <see cref="ScopesSupported"/> in the RFC 9728 protected-resource
    /// metadata at all. Set false for an authorization server that can grant nothing a client
    /// should ask for: advertising <c>teamcity:read</c> to one with no custom-scope capability
    /// makes a conforming client request a scope the server will refuse, failing the whole
    /// authorization request with <c>invalid_scope</c>.
    ///
    /// Prefer this over configuring a single blank scope entry. Both suppress the metadata, but a
    /// blank array element reads as a mistake, and an environment variable cannot express an empty
    /// array any other way.
    ///
    /// Advertising nothing is only correct when every client pins its own scopes locally. A client
    /// that requests no scopes at all cannot authenticate: an Okta org authorization server answers
    /// a scope-less authorize request with <c>invalid_scope</c> / "No scopes were requested."
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
