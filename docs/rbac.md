# Per-Caller RBAC (TeamCityRemoteMcpServer)

`Rbac:Enabled` (default `false`) authorizes each tool call against the *calling* user's own
TeamCity permissions, instead of every authenticated caller sharing the full reach of the server's
`TEAM_CITY_ACCESS_TOKEN`. It requires [OAuth authentication](authentication.md) — `Rbac:Enabled=true`
with `McpAuth:Enabled=false` is refused at startup, because without a validated caller identity
there is nothing to authorize.

> [!IMPORTANT]
> RBAC fails closed, and that is not configurable. Unresolved identity, a TeamCity 404, or an
> upstream error all deny. A misconfiguration shows up as every call being denied, not as open
> access — read [Rolling it out](#rolling-it-out) before enabling this in production.

## What changes when RBAC is on

### Authorization moves to the caller's own TeamCity permissions

Before a gated tool call runs, the server resolves the caller's identity (see
[Caller identity](#caller-identity)), maps it to a TeamCity user, and checks that user's actual
TeamCity permissions via `GET /app/rest/users/{locator}/permissions` — it does not reimplement or
guess at TeamCity's permission model, only passes through TeamCity's own answer.

### The server's TeamCity access token is still required

`TEAM_CITY_ACCESS_TOKEN` does not go away. RBAC changes *whose permissions decide* whether a call is
allowed — it does not change *whose token* calls the TeamCity REST API. The permission check itself,
and every underlying TeamCity API call a tool makes, still uses the server's own token.

### What is denied, what is filtered, and what is untouched

- **Denied outright**: a project-, build-, build-configuration-, or VCS-root-scoped tool call where
  the caller lacks the required permission on the resolved project. See
  [Per-tool permission requirements](#per-tool-permission-requirements) for exactly which permission
  each tool checks.
- **Filtered, not denied**: five cross-project tools —
  `teamcity_list_projects`, `teamcity_get_project_hierarchy`, `teamcity_search_builds`,
  `teamcity_get_test_history`, `teamcity_list_mutes` — narrow their *results* to the caller's visible
  projects rather than allowing or denying the whole call.
- **Not filtered**: the list of available tools (`tools/list`) is the same for every caller —
  RBAC narrows what a tool call can *return*, not which tools a client sees offered.
- **`teamcity_get_audit_log`** is a special case: it requires the caller's **global**
  `view_audit_log` permission on every call, even one scoped to a single project — because it can
  surface usernames and configuration-change details spanning the whole server. The exact
  permission id is best-effort and has not been confirmed against a live TeamCity instance — if a
  caller you expect to pass is denied here, check your server's actual permission id first.
- **`teamcity_server_info`** is never gated, by deliberate exception — it returns only the server
  version and build number, no project data.

## Caller identity

### The email-equals-TeamCity-username assumption

RBAC maps a caller to a TeamCity user by resolving an email address and matching it against a
TeamCity username. There is no other identity mapping available.

> [!IMPORTANT]
> If the caller's resolved email has no matching TeamCity user, every gated call for that caller is
> denied. Plus-addressed (`alice+work@example.invalid`) and other alias forms are rejected outright
> rather than normalized to a base address — resolving `alice+work@` to `alice@`'s TeamCity user
> would grant one person another person's authority on a guess.

### Identity from a token claim (`IdentitySource=Claim`, the default)

Reads the value of `Rbac:IdentityClaim` (default `"email"`) directly off the caller's validated
access token. This is the original, pre-RBAC behavior and requires nothing further — but only works
if your authorization server actually puts an email claim on its access tokens.

### Identity from OIDC `/userinfo` (`IdentitySource=UserInfo`)

For an authorization server whose access tokens carry no email claim at all (an Okta *org*
authorization server, for example) — without this, identity resolution returns nothing for every
caller and every gated call is denied while the server still reports healthy.

Calls the AS's OIDC `/userinfo` endpoint with the *caller's own* bearer token, so the server never
holds a standing credential against your identity provider — it can only read the profile of
whoever is calling it.

#### Guardrails applied to the address

Applied only on this path (the `Claim` path is left byte-identical to the original behavior):

- `email_verified` must be `true` in the `/userinfo` response.
- The address is lowercased before lookup, so one person can't occupy two identities.
- Plus-addressing and other alias forms are rejected, same as above.

#### Constraint: the `/userinfo` URL is not read from discovery

The `/userinfo` endpoint is derived as `{McpAuth:Issuer}/oauth2/v1/userinfo` — Okta's conventional
path — rather than read from the AS's own discovery document. There is no key to override this. If
your authorization server's userinfo endpoint is at a different path, `IdentitySource=UserInfo`
will boot healthy and deny every caller; only use it against an AS with this exact path shape.

## Quick start: RBAC with identity from a token claim

Builds on the [spec-conforming authentication quick start](authentication.md#quick-start-a-spec-conforming-authorization-server) — assumes your AS puts an `email` claim on its access tokens.

```bash
docker run -d \
  --name teamcity-mcp-server \
  -p 8080:8080 \
  -e TEAM_CITY_URL="https://teamcity.example.invalid" \
  -e TEAM_CITY_ACCESS_TOKEN="your_teamcity_access_token" \
  -e McpAuth__Enabled=true \
  -e McpAuth__Issuer="https://as.example.invalid/oauth2/default" \
  -e McpAuth__ResourceUri="https://mcp.example.invalid/mcp" \
  -e Rbac__Enabled=true \
  peakflames/teamcity-remote-mcp-server
```

Equivalent `appsettings.json` (in addition to the `McpAuth` block from the authentication quick
start):

```json
{
  "Rbac": {
    "Enabled": true
  }
}
```

`Rbac:IdentityClaim` defaults to `"email"` and `Rbac:IdentitySource` defaults to `Claim` — nothing
else to set for this path.

## Quick start: RBAC with identity from `/userinfo`

Builds on the [limited-authorization-server quick start](authentication.md#quick-start-an-authorization-server-without-per-resource-audiences-or-custom-scopes) — assumes your AS puts no email claim on its access tokens.

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
  -e Rbac__Enabled=true \
  -e Rbac__IdentitySource=UserInfo \
  peakflames/teamcity-remote-mcp-server
```

Equivalent `appsettings.json` (in addition to the `McpAuth` block from that quick start):

```json
{
  "Rbac": {
    "Enabled": true,
    "IdentitySource": "UserInfo"
  }
}
```

> [!IMPORTANT]
> `/userinfo` only returns claims for scopes the caller's token actually carries. The
> `ScopesSupported` list above (not `McpAuth__AdvertiseScopes=false`) is required here — without an
> `openid`/`email` scope on the token, `/userinfo` returns no usable email and every RBAC check is
> denied. See the [`AdvertiseScopes=false` fallback note](authentication.md#quick-start-an-authorization-server-without-per-resource-audiences-or-custom-scopes)
> for why it's unsuitable when paired with `IdentitySource=UserInfo`.

## Rolling it out

### Stage 1 — `Rbac:AuditOnly=true`

> [!WARNING]
> Audit-only does not enforce anything: every check still runs and is still recorded, but a deny
> never blocks the call.

Add `-e Rbac__AuditOnly=true` to either quick start above. Every gated call is evaluated and logged
exactly as it would be enforced, but nothing is actually blocked — the safe way to see what RBAC
*would* deny before anyone is affected by it.

### Stage 2 — reading the access-audit records

Each gated tool call produces one structured log record with: the caller's OAuth subject and client
id, the tool name, the resource and permission checked, the gate's **true** `Decision`
(`Allow`/`Deny`) and `DecisionReason`, and a separate `Blocked` flag recording whether the call was
actually stopped. `Decision`/`DecisionReason` always reflect the real verdict regardless of
`AuditOnly` — check those two fields to see what would happen once you enforce.

### Stage 3 — enforcing

Remove `Rbac__AuditOnly` (or set it to `false`). Denials now block the call.

## Per-tool permission requirements

Every RBAC-aware tool, transcribed from the authoritative map in
`src/TeamCityMcpTools/Rbac/ToolResourcePermissionMap.cs`:

| Tool | Resource argument | TeamCity permission | Enforcement |
|---|---|---|---|
| `teamcity_get_project` | `projectId` (required) | `view_project` | Denies on missing/malformed `projectId` |
| `teamcity_get_project_parameters` | `projectId` (required) | `view_project` | Denies on missing/malformed `projectId` |
| `teamcity_get_project_features` | `projectId` (required) | `view_project` | Denies on missing/malformed `projectId` |
| `teamcity_list_build_types` | `projectId` (optional) | `view_project` | Absent `projectId` allows unfiltered; malformed still denies |
| `teamcity_list_templates` | `projectId` (optional) | `view_project` | Absent `projectId` allows unfiltered; malformed still denies |
| `teamcity_list_vcs_roots` | `projectId` (optional) | `view_project` | Absent `projectId` allows unfiltered; malformed still denies |
| `teamcity_get_running_builds` | `projectId` (optional) | `view_project` | Absent `projectId` allows unfiltered; malformed still denies |
| `teamcity_get_queued_builds` | `projectId` (optional) | `view_project` | Absent `projectId` allows unfiltered; malformed still denies |
| `teamcity_get_vcs_root` | `vcsRootId` (required) | `view_project` | Resolved to owning project; pivot failure denies |
| `teamcity_get_build_type` | `buildTypeId` (required) | `view_project` | Resolved to owning project; pivot failure denies |
| `teamcity_get_build_type_parameters` | `buildTypeId` (required) | `view_project` | Resolved to owning project; pivot failure denies |
| `teamcity_get_build_type_features` | `buildTypeId` (required) | `view_project` | Resolved to owning project; pivot failure denies |
| `teamcity_list_builds` | `buildTypeId` (required) | `view_project` | Resolved to owning project; pivot failure denies |
| `teamcity_get_build` | `buildId` (required) | `view_project` | Resolved via build → build type → project; pivot failure denies |
| `teamcity_get_build_status` | `buildId` (required) | `view_project` | Resolved via build → build type → project |
| `teamcity_get_build_parameters` | `buildId` (required) | `view_project` | Resolved via build → build type → project |
| `teamcity_get_build_problems` | `buildId` (required) | `view_project` | Resolved via build → build type → project |
| `teamcity_get_build_changes` | `buildId` (required) | `view_project` | Resolved via build → build type → project |
| `teamcity_get_build_tests` | `buildId` (required) | `view_project` | Also cross-checks `view_project` on any other project a composite build's parts resolve to |
| `teamcity_get_build_log_failures` | `buildId` (required) | `view_build_runtime_data` | Resolved via build → build type → project |
| `teamcity_search_build_log` | `buildId` (required) | `view_build_runtime_data` | Resolved via build → build type → project |
| `teamcity_list_build_artifacts` | `buildId` (required) | `view_project` | Resolved via build → build type → project |
| `teamcity_get_build_artifact_content` | `buildId` (required) | `view_file_content` | Returns raw artifact bytes — the highest-priority tool to gate |
| `teamcity_get_build_dependency_tree` | `buildId` (required) | `view_project` | Also cross-checks `view_project` on every project a dependency/dependent node resolves to |
| `teamcity_get_build_type_dependency_graph` | `buildTypeId` (required) | `view_project` | Also cross-checks `view_project` on every project a dependency/dependent node resolves to |
| `teamcity_list_projects` | — | `view_project` | Results filtered to the caller's visible projects |
| `teamcity_get_project_hierarchy` | — | `view_project` | Results filtered to the caller's visible projects |
| `teamcity_search_builds` | — | `view_project` | Results filtered to the caller's visible projects |
| `teamcity_get_test_history` | — | `view_project` | Results filtered to the caller's visible projects |
| `teamcity_list_mutes` | — | `view_project` | Results filtered to the caller's visible projects |
| `teamcity_get_audit_log` | `affectedProjectId` (optional, does not narrow the check) | `view_audit_log` (global; best-effort id, unconfirmed against a live server) | Requires the **global** permission on every call |
| `teamcity_server_info` | — | — | Never gated |

At startup, with `Rbac:Enabled=true`, the server logs how many of these tools are actually enforced
this session — check that log line if a permission you expect to be checked doesn't seem to apply
to your running version.

Audit reason ids you may see in logs when identity resolution fails (all safe to grep for, never a
credential): `identity_unresolved`, `identity_no_bearer_token`, `identity_email_unverified`,
`identity_email_rejected_form`, `identity_userinfo_rate_limited` (the alertable one — your
authorization server's shared `/userinfo` quota is being throttled, logged at Error level, not a
statement about any one caller), `identity_userinfo_unavailable`.

## Configuration reference: `Rbac`

| Key | Type | Default | Effect |
|---|---|---|---|
| `Enabled` | bool | `false` | Master switch. Requires `McpAuth:Enabled=true` — refused at startup otherwise. |
| `AuditOnly` | bool | `false` | `true`: every check still runs and is audited, but a deny never blocks the call. |
| `IdentityClaim` | string | `"email"` | Claim name read off the token when `IdentitySource=Claim`. |
| `IdentitySource` | enum | `Claim` | `Claim` or `UserInfo` — see [Caller identity](#caller-identity). |
| `UserInfoCacheTtlSeconds` | int | `300` | Ceiling on how long a `/userinfo` response is reused. Range `1`–`86400`. |
| `PermissionCacheTtlSeconds` | int | `120` | How long a TeamCity permission check result is reused. Range `1`–`3600`. |
| `VisibleSetCacheTtlSeconds` | int | `600` | How long a caller's visible-project set is reused. Range `1`–`86400`. |
| `IdentityCacheTtlSeconds` | int | `300` | How long a resolved caller→TeamCity-user mapping is reused. Range `1`–`86400`. |
| `BuildTypeProjectCacheTtlSeconds` | int | `900` | How long a build-type→project pivot is reused. Range `1`–`86400`. |
| `BuildProjectCacheTtlSeconds` | int | `3600` | How long a build→project pivot is reused. Range `1`–`86400`. |
| `MaxCacheEntries` | int | `20000` | Capacity bound shared by the permission, identity, and both pivot caches. |
| `MaxVisibleSetCacheEntries` | int | `2000` | Capacity bound for the visible-project-set cache (entries are much larger — up to thousands of project ids each). |

This table, not the checked-in `appsettings.json`, is the complete key list.

`FailClosed` is not a key — it is always true and cannot be relaxed. `AuditOnly` is the only lever
that changes what happens after a deny is computed.

### Cache TTLs, bounds, and the revocation window

| Cache | TTL | What you accept |
|---|---|---|
| Permission (`PermissionCacheTtlSeconds`, default `120`) | up to 120s | A TeamCity permission revoked mid-flight is still honored by this server for up to this long |
| `/userinfo` (`UserInfoCacheTtlSeconds`, default `300`) | `min(remaining token lifetime, this)` | Can never outlive the token it was fetched with — a revoked token stops resolving as soon as it would have expired anyway |
| Identity (`IdentityCacheTtlSeconds`, default `300`) | up to 300s | A caller→TeamCity-user mapping change (e.g. a TeamCity username rename) takes up to this long to apply |
| Build-type→project pivot (`BuildTypeProjectCacheTtlSeconds`, default `900`) | up to 900s | Deliberately shorter than the build→project pivot below — build configurations do move between projects in live TeamCity usage |
| Build→project pivot (`BuildProjectCacheTtlSeconds`, default `3600`) | up to 3600s | Longer-lived because a build never moves to a different build configuration once it exists |
| Visible project set (`VisibleSetCacheTtlSeconds`, default `600`) | up to 600s | A newly granted or revoked project visibility takes up to this long to apply to cross-project tool results |

All caches are swept for expired entries every 30 seconds (not configurable). The `/userinfo` HTTP
call itself times out after 10 seconds (not configurable) — a slow identity provider fails fast into
a denial rather than holding the caller's request open.

## Startup validation errors

`Rbac` is validated at startup, but only when `Rbac:Enabled=true`.

| Message | Cause | Fix |
|---|---|---|
| `Rbac:Enabled requires McpAuth:Enabled — …` | RBAC on, authentication off | Enable `McpAuth` first, or leave `Rbac` disabled |
| `Rbac:IdentityClaim must not be blank.` | `IdentityClaim` is empty | Set it to a real claim name (default `"email"`) |
| `Rbac:IdentitySource=UserInfo requires McpAuth:Issuer — …` | `IdentitySource=UserInfo` with a blank `McpAuth:Issuer` | Set `McpAuth:Issuer` — the `/userinfo` URL is derived from it |
| `Rbac:UserInfoCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:PermissionCacheTtlSeconds must be between 1 and 3600.` | value out of range (note the tighter ceiling) | Pick a value in range |
| `Rbac:VisibleSetCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:IdentityCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:BuildTypeProjectCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:BuildProjectCacheTtlSeconds must be between 1 and 86400.` | value out of range | Pick a value in range |
| `Rbac:MaxCacheEntries must be between 1 and 10000000.` | value out of range | Pick a value in range |
| `Rbac:MaxVisibleSetCacheEntries must be between 1 and 10000000.` | value out of range | Pick a value in range |

## Troubleshooting

**Every tool call is denied, or projects come back empty** — RBAC fails closed. Check, in order:
(1) does the caller's resolved email have a matching TeamCity username, (2) does that TeamCity user
actually have `view_project` (or the specific permission from the table above) on the resource being
accessed, (3) if using `IdentitySource=UserInfo`, is your AS's userinfo endpoint really at
`{Issuer}/oauth2/v1/userinfo`. Start with `Rbac:AuditOnly=true` and read the access-audit records'
`DecisionReason` field — it names exactly which of these failed.

**Server refuses to start with a `Rbac` validation message** — see the table above.
