# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `TeamCityRemoteMcpServer` is now a pure OAuth 2.1 **resource server** — no embedded authorization
  server. `McpAuth:Issuer` points at an external AS; JwtBearer fetches its discovery document and
  JWKS via `Authority` (with automatic key-rotation refresh) rather than this server holding or
  resolving signing keys itself. Gated behind `McpAuth:Enabled` (default `false`, byte-identical
  anonymous behavior when unset). The SDK auto-serves
  `/.well-known/oauth-protected-resource/mcp` (RFC 9728) and emits the
  `WWW-Authenticate: Bearer resource_metadata=...` challenge on `POST /mcp`; token validation pins
  `RS256`, and checks issuer/audience/lifetime against the configured external AS. A fail-to-boot
  options validator (`McpAuth:*`) covers the schema (`Issuer`, `MetadataAddress`, `ResourceUri`,
  `ScopesSupported`, `ClockSkewSeconds`) and allows a Custom-AS-style issuer that has a path. Adds
  `tests/StubAuthorizationServer/`, a dependency-free stub external AS (discovery, JWKS,
  `client_credentials` token issuance) for local development and the test suite.
- Per-caller RBAC. Ships `TeamCityMcpTools/Rbac/` (`IPermissionGate`, `NoOpPermissionGate`,
  `ToolGate`/`ToolGateSession`, and a hand-written `FrozenDictionary` `ToolResourcePermissionMap`
  covering all 32 tools across permission groups G1-G6), a `TeamCityToolNames` const class now used
  by every `[McpServerTool(Name = ...)]` attribute instead of a string literal, and
  `TeamCityRemoteMcpServer/Rbac/` (`Rbac:Enabled`/`Rbac:AuditOnly` config surface, a
  `TeamCityIdentityResolver` resolving a JWT claim to a TeamCity user id via `email:`/`username:`
  lookup, an `AsyncLocal`-backed `IRbacCallContextAccessor` making that identity visible from the
  sibling DI scope each tool opens via `CreateAsyncScope()`, a central `RbacIdentityFilter`
  registered via `McpServerOptions.Filters.Request.CallToolFilters`, and a
  `SerilogMcpAccessAuditSink` emitting one `AccessAuditRecord` per `tools/call`). `Rbac:Enabled=true`
  with `McpAuth:Enabled=false` fails to boot — RBAC without authentication means no identity for any
  caller. `teamcity_server_info` is mapped as a deliberate `Ungated` exception (G6), not an omission.
- Real RBAC enforcement, replacing the always-allow placeholder gate. `TeamCityPermissionGate`
  queries TeamCity's own `GET /app/rest/users/{locator}/permissions` (`count>=1` rule; batching,
  inheritance, and global-grant behavior verified live against a TeamCity instance), backed by a
  capacity-bounded, single-flight, never-caches-errors `TtlCache<TKey,TValue>`
  (`Rbac/Caching/`). Both the permission cache (`Rbac:PermissionCacheTtlSeconds`, default 120s) and
  a caching decorator over identity resolution (`CachingIdentityResolver`,
  `Rbac:IdentityCacheTtlSeconds` positive / fixed 30s negative TTL) are wired in, evicted
  periodically by `RbacCacheJanitor`. Enforces the tools with a project-scoped argument (required or
  optional); the remainder allow-and-audit as explicitly deferred, tracked per-tool as required map
  data (`GateEnforcement`) rather than inferred from resource kind, so an unmapped tool is a compile
  error and a map miss fail-closed denies at runtime. Extracts the decision logic into
  `RbacGateDecider` (pure, unit-testable, no HTTP) and fixes two related bugs: a non-string
  `projectId` argument (e.g. `{"projectId": 123}`) no longer silently falls through to an unchecked
  allow, and project-scoped tools no longer pass a `buildId`/`buildTypeId` into a project-scoped
  permission check. Fixes an `AuditOnly` shadow-mode defect where a would-be deny was logged as an
  `Allow` — `AccessAuditRecord` now carries the gate's true `Decision`/`DecisionReason` plus a
  separate `Blocked` field, and the audit record is written before `next(...)` runs so a throwing
  tool body doesn't lose it. Fixes a captive-dependency bug in both the gate and
  `TeamCityIdentityResolver` (each was becoming a singleton pinning one `HttpClient` handler chain
  for process life) by resolving `ITeamCityClientFactory` from a fresh `CreateAsyncScope()` per
  upstream call. A startup warning enumerates the unenforced-tool gap.
- Fixes RBAC identity resolution: TeamCity returns user ids as a bare JSON number, not a string.

### Changed
- Bumped `ModelContextProtocol`/`ModelContextProtocol.AspNetCore` from 1.2.0 to 2.1.0
- `TeamCityRemoteMcpServer` now runs the MCP HTTP transport in stateless mode (2.x default)
- `TeamCityClient` now uses a pooled `HttpClient` via `IHttpClientFactory` instead of constructing a new `HttpClient` per call, and no longer performs a preflight `GET app/rest/server` check on every tool invocation

### Removed
- **BREAKING:** The legacy `/sse` and `/message` SSE transport endpoints are gone from `TeamCityRemoteMcpServer`. Clients must use the Streamable HTTP transport at `/mcp`. The root `/` MCP mount is also removed — `/mcp` is now the only mount point

## [0.3.0] - 2026-07-21

### Added
- `teamcity_get_build_dependency_tree` tool to walk a build's actual snapshot-dependency chain, with a `format` option (`markdown`/`mermaid`) rendering a `graph LR` diagram — dependencies (upstream) on the left, dependents (downstream) on the right, arrows flowing dependency -> dependent, matching the TeamCity build-chain UI
- `teamcity_get_build_type_dependency_graph` tool to render a build type's design-time dependency/dependent graph, with the same `mermaid` format option combining dependencies/dependents into one graph with the build type in the middle
- `teamcity_get_build_tests` tool to retrieve test occurrences and failure detail for a build. Composite/matrix builds are handled natively — TeamCity aggregates test occurrences across the whole chain, so results already cover every sub-build, with a Sub-build column and a Chain Parts section giving accurate per-sub-build totals. Summary counts (including a new `newFailed` count) are pulled from the build's own totals rather than the page-scoped test-occurrences response, so they're accurate regardless of `count`. The default `failed` filter excludes muted tests (`status:FAILURE,muted:false`) to match what the TeamCity UI reports. Test durations are shown in both humanized (h/m/s) and raw-millisecond form
- `teamcity_get_test_history` tool to follow a single test's status across builds, with durations shown in both humanized (h/m/s) and raw-millisecond form
- `teamcity_get_build_type_features` tool to retrieve a build configuration's build features (e.g. the Matrix Build feature, build failure conditions, swabra, notifications), grouped by type and marked own or inherited
- `teamcity_get_build_problems` tool to retrieve dedicated build problem occurrences (exit codes, snapshot dependency failures, etc.)
- `teamcity_list_build_artifacts` tool to list a build's artifact files and directories, including hidden `.teamcity/...` entries
- `teamcity_get_build_artifact_content` tool to read a text artifact's content, with binary refusal and size/line truncation guardrails
- `teamcity_get_build_parameters` tool to retrieve a build's resulting configuration parameters, with name-filtering and a display cap
- `teamcity_get_build_changes` tool to retrieve the VCS changes (commits) included in a build
- `teamcity_get_audit_log` tool to retrieve TeamCity audit log entries, optionally scoped to a build type or project
- `teamcity_get_vcs_root` tool to retrieve full connection details (repo URL, branch spec, auth method) for a single VCS root, with secure properties masked
- `teamcity_list_vcs_roots` tool to list VCS roots (id and name), optionally scoped to a project or filtered by name
- `teamcity_list_mutes` tool to retrieve mute details (reason, who muted it and when, scope, resolution policy) from `/app/rest/mutes`, scoped by project, build type, or a build ID resolved to its build type
- `teamcity_search_build_log` tool to search a build's full console log server-side for a regex or literal pattern, returning merged context windows around matches
- `teamcity_get_build_log_failures` tool to combine a build's failed-test details with log context windows keyed to each failed test's name, for one-call failure diagnosis
- `teamcity_get_project_parameters` tool to retrieve a project's configuration parameters, marked own or inherited from a parent project, with substring name filtering
- `teamcity_get_project_features` tool to retrieve a project's features (report tabs, versioned settings, issue trackers, etc.), grouped by type and marked own or inherited
- `teamcity_list_templates` tool to list build templates, optionally scoped to a project, with name/ID regex filtering and a result count cap
- `teamcity_get_build_type_parameters` tool to retrieve a build configuration's parameters, marked own or inherited from a template, with substring name filtering

### Changed
- `teamcity_get_build` now also returns the `personal` flag and, per VCS revision, the associated VCS root instance/root ID
- `teamcity_get_build` now surfaces the `composite` flag (whether the build is a matrix/build-chain build) and renders `duration` in both humanized (h/m/s) and raw-second form instead of a bare `MM:SS` string
- `teamcity_get_build_type` now includes a compact Build Features section (type/id/origin/disabled), pointing to `teamcity_get_build_type_features` for full property detail
- `teamcity_get_build_dependency_tree`, `teamcity_get_build_type_dependency_graph`, and `teamcity_get_build_type` now also surface artifact dependencies (previously only snapshot), labeled `[snapshot]`/`[artifact]`/`[snapshot+artifact]`. Artifact dependents remain unresolvable via the TeamCity API
- TeamCity's run-level `artifact-dependencies` field on a build is often unpopulated even for a resolved, successful dependency — `teamcity_get_build_dependency_tree` now flags this and points to the design-time tools as a cross-check
- `teamcity_get_build_tests` and `teamcity_get_build_log_failures` now group failed tests sharing byte-identical failure text into a single "Failure Groups" section instead of repeating it per test
- Both tools add a `detailsMode` (`compact`/`full`/`none`) and `detailsMaxChars` parameter to control how much failure text is shown per group
- All markdown-returning tools now clamp their output to a safe character ceiling as a safety net, with a note when truncation occurs, so no single tool call can exceed the MCP client's token limit
- Lowered `teamcity_search_build_log`'s and `teamcity_get_build_artifact_content`'s output byte caps to stay under the new shared ceiling
- `teamcity_get_project` now includes a Build Templates section
- `teamcity_get_build_type` now marks every section — templates, settings, VCS roots, triggers, steps, dependencies — as own or inherited from a template
- `teamcity_get_audit_log` adds a `configChangesOnly` filter to show only real configuration edits, cutting through build-queue noise
- `teamcity_list_build_types` and `teamcity_list_projects` now support `nameFilter` (regex on Name), `idFilter` (regex on ID), and a `count` cap, plus output clamping

### Fixed
- `teamcity_get_build` now shows the human-readable VCS root name instead of the internal VCS root ID
- `teamcity_get_build` now correctly reports a revision's branch (was always showing "unknown branch" due to a wrong field name)
- `teamcity_get_build_tests` and `teamcity_get_build_log_failures` no longer return unbounded output for builds with many failed tests or large captured test output — both previously had no output budget on a test's failure text and could exceed 200,000 characters in a single call
- `teamcity_search_builds` no longer 404s when `projectId` points at a project with no build configurations of its own (only subprojects) — the locator now uses `affectedProject` instead of `project` so it recurses into subprojects

## [0.2.0] - 2026-04-27

### Added
- `teamcity_list_projects` tool to list all TeamCity projects
- `teamcity_get_project` tool to retrieve details of a specific project
- `teamcity_get_project_hierarchy` tool to navigate the full project hierarchy
- `teamcity_list_build_types` tool to list build configurations within a project
- `teamcity_get_build_type` tool to retrieve build configuration details including steps, triggers, and agent requirements
- `teamcity_get_build_status` tool to check the status of a specific build
- `teamcity_get_queued_builds` tool to list builds currently in the queue
- `teamcity_get_running_builds` tool to list builds currently running
- `teamcity_search_builds` tool to search builds with filtering options
- `teamcity_server_info` tool to retrieve TeamCity server information
- Enriched build and build-type models with steps, triggers, and agent info

## [0.1.0] - 2026-04-23

### Added
- Initial release of TeamCity MCP Servers
- Support for TeamCity CI/CD integration via MCP (Model Context Protocol)
- `list_builds` tool to get builds for a given build type/configuration
- `get_build_details` tool to retrieve detailed information about a specific build
- TeamCityRemoteMcpServer (ASP.NET Web API-based) with Streamable HTTP and SSE transport
- TeamCityMcpServer (Console-based) with stdio transport
- TeamCity HTTP client with Bearer token authentication and factory pattern
- Docker support with built-in .NET SDK container capabilities
- GitHub Actions workflow for automated Docker Hub publishing on version tag push
- Environment variable support for secure credential management
