# OAuth 2.1 Authentication (TeamCityRemoteMcpServer)

`TeamCityRemoteMcpServer` can require a bearer token issued by an external OAuth 2.1 authorization
server, instead of accepting anonymous `POST /mcp` calls. It's opt-in (`McpAuth:Enabled`, default
`false`) — with it unset, the server's behavior is unchanged from every prior release.

This page covers the `TeamCityRemoteMcpServer` (Docker / HTTP) deployment only. The stdio
`TeamCityMcpServer` console app has no concept of a caller and is unaffected by anything here.

## Before you start

### What this server does and does not do

- It validates every bearer token against the authorization server (AS) you configure — checking
  signature, issuer, audience, expiry, and (by default) a `teamcity:read` scope — before a tool call
  is allowed to run.
- It does not run its own authorization server, issue tokens, or store credentials for your users.
  You bring an existing AS (Okta, Auth0, Keycloak, an internal OIDC provider, …).
- It does not change *whose* TeamCity permissions a tool call uses — every call still runs with the
  server's own `TEAM_CITY_ACCESS_TOKEN`. To authorize each call against the *calling* user's own
  TeamCity permissions instead, see [Per-Caller RBAC](rbac.md), a separate, additional opt-in.
- Once enabled, it publishes [RFC 9728](https://www.rfc-editor.org/rfc/rfc9728) protected-resource
  metadata at `/.well-known/oauth-protected-resource/mcp`, so a conforming MCP client can discover
  where to authenticate without being told out of band.

### What your authorization server must support

Not every authorization server can mint a per-resource audience or grant a custom scope. Answer
these three questions before configuring anything — they decide which quick start below applies:

| Can your AS… | Yes | No |
|---|---|---|
| mint an access token whose `aud` is *your* resource URI? | leave `ValidateAudience` at its default (`true`) | `ValidateAudience=false`, **plus** `AllowedClientIds` |
| grant a custom scope (`teamcity:read`)? | leave `RequireScope`/`ScopesSupported` at their defaults | `RequireScope=false`, and set `ScopesSupported` to what it *can* grant, or `AdvertiseScopes=false` |
| put the caller's email on the access token? | (only relevant if you also enable RBAC) `Rbac:IdentitySource=Claim` | `Rbac:IdentitySource=UserInfo` — see [rbac.md](rbac.md) |

An Okta **Custom** Authorization Server answers "yes" to both rows; an Okta **org** authorization
server answers "no" to both, because it always stamps `aud` with its own issuer and cannot grant a
custom scope. If you don't know which you have, start with the first quick start below and let the
startup validator tell you if it's wrong.

## Quick start: a spec-conforming authorization server

Assumes your AS can mint a token with `aud` set to your resource URI and can grant `teamcity:read`.

### Configuration

```bash
docker run -d \
  --name teamcity-mcp-server \
  -p 8080:8080 \
  -e TEAM_CITY_URL="https://teamcity.example.invalid" \
  -e TEAM_CITY_ACCESS_TOKEN="your_teamcity_access_token" \
  -e McpAuth__Enabled=true \
  -e McpAuth__Issuer="https://as.example.invalid/oauth2/default" \
  -e McpAuth__ResourceUri="https://mcp.example.invalid/mcp" \
  peakflames/teamcity-remote-mcp-server
```

Equivalent `appsettings.json`:

```json
{
  "McpAuth": {
    "Enabled": true,
    "Issuer": "https://as.example.invalid/oauth2/default",
    "ResourceUri": "https://mcp.example.invalid/mcp"
  }
}
```

Every other `McpAuth` key stays at its default: `ScopesSupported=["teamcity:read"]`,
`AdvertiseScopes=true`, `ValidateAudience=true`, `RequireScope=true`.

### Verify

```bash
curl -s https://mcp.example.invalid/.well-known/oauth-protected-resource/mcp
```

Confirm `resource` matches `ResourceUri`, `authorization_servers` contains `Issuer`, and
`scopes_supported` is `["teamcity:read"]`. A `POST /mcp` with no `Authorization` header should now
get a `401` carrying a `WWW-Authenticate` header that points at this metadata URL, instead of
succeeding.

### Point an MCP client at it

A client that implements MCP's OAuth discovery flow (RFC 9728 + OAuth 2.1 dynamic client
registration or a pre-registered client) will fetch the metadata above and start an authorization
flow automatically the first time it connects — see [README.md](../README.md#configuring-mcp-clients)
for client-specific setup.

## Quick start: an authorization server without per-resource audiences or custom scopes

Assumes your AS always stamps `aud` with its own issuer (never a per-resource value) and cannot
grant a custom scope — the shape of an Okta **org** authorization server.

### Why the defaults do not work here

With `ValidateAudience` at its default `true`, every token is rejected — its `aud` will never equal
`ResourceUri`. With `RequireScope` at its default `true`, every token is rejected again — the AS has
no way to grant `teamcity:read`. And with `AdvertiseScopes`/`ScopesSupported` at their defaults, a
client asking for `teamcity:read` gets `invalid_scope` back from the AS before it ever reaches this
server.

The substitute for audience binding is a client-id allowlist: it doesn't prove a token was minted
*for* this resource, only that it was minted for an OAuth client you recognize.

### Configuration

```bash
docker run -d \
  --name teamcity-mcp-server \
  -p 8080:8080 \
  -e TEAM_CITY_URL="https://teamcity.example.invalid" \
  -e TEAM_CITY_ACCESS_TOKEN="your_teamcity_access_token" \
  -e McpAuth__Enabled=true \
  -e McpAuth__Issuer="https://as.example.invalid" \
  -e McpAuth__ResourceUri="https://mcp.example.invalid/mcp" \
  -e McpAuth__ValidateAudience=false \
  -e McpAuth__AllowedClientIds__0="0oaEXAMPLECLIENTID" \
  -e McpAuth__RequireScope=false \
  -e McpAuth__ScopesSupported__0=openid \
  -e McpAuth__ScopesSupported__1=email \
  -e McpAuth__ScopesSupported__2=profile \
  -e McpAuth__ScopesSupported__3=offline_access \
  peakflames/teamcity-remote-mcp-server
```

Equivalent `appsettings.json`:

```json
{
  "McpAuth": {
    "Enabled": true,
    "Issuer": "https://as.example.invalid",
    "ResourceUri": "https://mcp.example.invalid/mcp",
    "ValidateAudience": false,
    "AllowedClientIds": [ "0oaEXAMPLECLIENTID" ],
    "RequireScope": false,
    "ScopesSupported": [ "openid", "email", "profile", "offline_access" ]
  }
}
```

This assumes your AS can still grant OIDC scopes, just nothing custom to this server — the common
case, and the one required if you intend to pair this with
[`Rbac:IdentitySource=UserInfo`](rbac.md#quick-start-rbac-with-identity-from-userinfo), since
`/userinfo` only returns claims for scopes the token actually carries.

> [!NOTE]
> If your AS cannot grant *any* scope at all — not even standard OIDC ones — set
> `McpAuth__AdvertiseScopes=false` instead of `ScopesSupported` above. This publishes an empty
> `scopes_supported` rather than advertising scopes the AS would reject. Do not combine this with
> `Rbac:IdentitySource=UserInfo`: without an `openid`/`email` scope on the token, `/userinfo` will
> not return a usable email and every RBAC check will be denied.

> [!WARNING]
> **`ValidateAudience=false`** accepts any token your AS issued to an allowlisted client, for any
> purpose — it does not prove the token was minted *for this server*. `AllowedClientIds` is the
> required compensating control, and the server refuses to start if it's empty while
> `ValidateAudience` is `false`.

> [!WARNING]
> **`RequireScope=false`** means a valid token proves *who* the caller is, not *what* they may do —
> scope no longer limits anything. Pair this with [RBAC](rbac.md), or every authenticated caller
> gets the full reach of the server's own TeamCity access token.

See [`ScopesSupported` replaces the default](#scopessupported-replaces-the-default-it-does-not-append)
below for how the list above is bound.

### Verify

```bash
curl -s https://mcp.example.invalid/.well-known/oauth-protected-resource/mcp
```

Confirm `scopes_supported` is `["openid", "email", "profile", "offline_access"]` (or `[]` if you
used the `AdvertiseScopes=false` fallback) — a client requesting `teamcity:read` against an AS that
cannot grant it fails the whole authorization request with `invalid_scope` before ever reaching
this server.

## Configuration reference: `McpAuth`

| Key | Type | Default | Effect |
|---|---|---|---|
| `Enabled` | bool | `false` | Master switch. `false` skips all auth registration — no options binding, no JWT validation, no metadata endpoint, `POST /mcp` stays open. |
| `Issuer` | string | `""` | The external AS's issuer URI. Becomes `Authority` for OIDC discovery/JWKS fetch (with automatic key-rotation) and the token's expected `iss`. |
| `MetadataAddress` | string? | `null` | Explicit override for the discovery-document URL, for an AS whose discovery document isn't at the conventional `{Issuer}/.well-known/openid-configuration`. |
| `ResourceUri` | string | `""` | This server's own resource identifier — both the expected JWT `aud` and the RFC 9728 `resource` value, e.g. `https://mcp.example.invalid/mcp`. |
| `ScopesSupported` | string list | `["teamcity:read"]` | Scopes advertised in the RFC 9728 metadata, and therefore what a conforming client requests. **Replaces**, not appends — see below. |
| `AdvertiseScopes` | bool | `true` | `false` publishes an empty `scopes_supported`, for an AS that can grant nothing custom to this server. |
| `ClockSkewSeconds` | int | `30` | Allowed clock drift for token expiry/not-before checks. Valid range `0`–`300`. |
| `ValidateAudience` | bool | `true` | `false` stops binding `aud` to `ResourceUri`. Requires a non-empty `AllowedClientIds` — refused at startup otherwise. |
| `AllowedClientIds` | string list | `[]` | Client-id allowlist, matched against the token's `cid` claim. The substitute for audience binding. |
| `RequireScope` | bool | `true` | `false` drops the `teamcity:read` assertion from the authorization policy. |

This table, not the checked-in `appsettings.json`, is the complete key list — that file is a
non-secret starting point, not an exhaustive reference.

Non-configurable, worth knowing: only `RS256`-signed tokens are accepted; the `sub` claim is never
remapped (`MapInboundClaims=false`); protected-resource metadata is served automatically at
`/.well-known/oauth-protected-resource/mcp` (there's no key to change the path); `jwks_uri` is
deliberately omitted from that metadata (RFC 9728's `jwks_uri` means *resource-response* signing,
not token signing); and `RequireHttpsMetadata` is derived from `Issuer`'s own scheme, not from the
hosting environment.

### Setting these as environment variables

`McpAuth:Key` becomes `McpAuth__Key` (double underscore) as an environment variable — standard
ASP.NET Core configuration binding. List entries are indexed:
`McpAuth__AllowedClientIds__0`, `McpAuth__AllowedClientIds__1`, `McpAuth__ScopesSupported__0`, …

### `ScopesSupported` replaces the default, it does not append

Configuring `ScopesSupported` **replaces** the default list rather than adding to it — ordinary
.NET configuration binding would otherwise append to the pre-populated `["teamcity:read"]` default,
so setting `McpAuth__ScopesSupported__0=openid` would advertise `["teamcity:read", "openid"]`
instead of `["openid"]`. This server corrects that: a configured list is advertised verbatim.

An environment variable also cannot express an empty array — there's no way to write
`McpAuth__ScopesSupported=[]`. That's the reason `AdvertiseScopes` exists as its own flag: set it to
`false` to publish nothing, rather than trying to configure a blank scope entry (which is dropped
anyway, since a blank string is never a scope a client could legitimately request).

## Startup validation errors

`McpAuth` is validated at startup, but only when `McpAuth:Enabled=true` — a malformed section can
never break a server that has auth off. Every failure below is accumulated and reported together,
not one at a time.

| Message | Cause | Fix |
|---|---|---|
| `Issuer must be an absolute URI.` | `Issuer` blank or not a full URI | Set `McpAuth:Issuer` to your AS's full issuer URL |
| `Issuer must use https (http is only allowed in the Development environment).` | `Issuer` is `http://` outside Development | Use `https://`, or set `ASPNETCORE_ENVIRONMENT=Development` for local testing only |
| `Issuer must not contain a fragment.` | `Issuer` has a `#` | Remove the fragment (a path, e.g. `/oauth2/<asid>`, is fine) |
| `ResourceUri must be an absolute URI.` | `ResourceUri` blank or not a full URI | Set `McpAuth:ResourceUri` to this server's own URL |
| `ResourceUri must not contain a fragment.` | `ResourceUri` has a `#` | Remove the fragment |
| `MetadataAddress must be an absolute URI.` | `MetadataAddress` is set but not a full URI | Fix the URL, or unset the key to use the default discovery location |
| `MetadataAddress must use https (http is only allowed in the Development environment).` | `MetadataAddress` is `http://` outside Development | Use `https://`, or set `ASPNETCORE_ENVIRONMENT=Development` |
| `ClockSkewSeconds must be between 0 and 300.` | value outside `0`–`300` | Pick a value in range |
| `AllowedClientIds must contain at least one client id when ValidateAudience is false — …` | `ValidateAudience=false` with an empty `AllowedClientIds` | Add at least one client id, or leave `ValidateAudience` at `true` |
| `AllowedClientIds[{i}] must not be blank.` | a blank/whitespace entry in `AllowedClientIds` | Remove the blank entry |

## Tradeoffs you are accepting

| Setting | What you accept | Required compensating control |
|---|---|---|
| `ValidateAudience=false` | A token minted for *any* purpose by an allowlisted client is accepted — not proven to be minted for this server | `AllowedClientIds` (enforced at startup) |
| `RequireScope=false` | Scope no longer limits what an authenticated caller can do | [Per-caller RBAC](rbac.md), or accept every caller gets the server token's full reach |

## Troubleshooting

**Server refuses to start with an `McpAuth` validation message** — see the table above; every
message states exactly which key to fix.

**`POST /mcp` returns 401 and the client never prompts to log in** — confirm the client actually
implements MCP's OAuth discovery flow (RFC 9728). Fetch
`/.well-known/oauth-protected-resource/mcp` yourself and check it answers with your `Issuer` and
`ResourceUri` — if that 404s, `McpAuth:Enabled` isn't actually `true` on the running server.

**A previously-valid token is now rejected** — check `ClockSkewSeconds` if it's a near-expiry token,
and confirm the AS hasn't rotated its signing keys faster than JWKS discovery can refresh (rare;
report it if reproducible).

## Next: per-caller authorization

Authentication only proves *who* is calling. To authorize each tool call against the *calling*
user's own TeamCity permissions — instead of every authenticated caller sharing the server's full
access — see [Per-Caller RBAC](rbac.md).
