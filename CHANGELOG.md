# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- OAuth 2.1 bearer-token authentication for `TeamCityRemoteMcpServer`, disabled by default
  (`McpAuth:Enabled`, default `false` — behavior is unchanged when unset)
- `McpAuth:Issuer` config to point the server at an external authorization server; the server
  validates tokens against it and publishes protected-resource metadata at
  `/.well-known/oauth-protected-resource/mcp` so MCP clients can discover where to authenticate
- Per-caller RBAC (`Rbac:Enabled`, default `false`), authorizing each tool call against the
  calling user's own TeamCity permissions instead of the server's access token. Requires
  `McpAuth:Enabled=true`
- `Rbac:AuditOnly` mode to log what would be denied without blocking anything, for staged rollout
- An access-audit record per tool call — caller, tool, and authorization decision — when RBAC is on
- `McpAuth:ValidateAudience`, default `true`, to relax audience binding for an authorization server
  that cannot mint a per-resource `aud`. Requires at least one `McpAuth:AllowedClientIds` entry,
  enforced at startup
- `McpAuth:AllowedClientIds`, default empty, to allowlist the OAuth client ids permitted to call
  this server, matched against the access token's `cid` claim
- `McpAuth:RequireScope`, default `true`, to drop the `teamcity:read` scope assertion for an
  authorization server with no custom-scope capability
- `McpAuth:AdvertiseScopes`, default `true`, to publish an empty `scopes_supported` in the
  protected-resource metadata for deployments where every client pins its own scopes locally
- `Rbac:IdentitySource` (`Claim`, the default, or `UserInfo`) to resolve the caller's email from
  the authorization server's OIDC `/userinfo` endpoint when its access tokens carry no email claim
- RBAC enforcement for every build-, build-configuration-, and VCS-root-scoped tool, resolving the
  named resource to its owning project and checking the caller's `view_project` there
- Visible-project filtering for `teamcity_list_projects`, `teamcity_get_project_hierarchy`,
  `teamcity_search_builds`, `teamcity_get_test_history`, and `teamcity_list_mutes` — results are
  narrowed to the caller's projects rather than the whole call being allowed or denied
- [docs/authentication.md](docs/authentication.md) and [docs/rbac.md](docs/rbac.md), covering both
  config sections and their deployment modes

### Changed
- MCP SDK upgraded to 2.1.0; the HTTP transport now runs in stateless mode
- Tool calls are faster — the TeamCity client reuses pooled connections and no longer makes a
  preflight server check on every call
- `McpAuth:ScopesSupported` now replaces the default rather than appending to it — a configured
  list is advertised verbatim, where previously `teamcity:read` remained in the advertised set
- `teamcity_get_audit_log` requires the caller's global `view_audit_log` permission when RBAC is
  on, on every call including one scoped to a project

### Removed
- **BREAKING:** the `/sse` and `/message` SSE endpoints and the root `/` mount are gone. `/mcp`
  (Streamable HTTP) is the only endpoint — update any client still pointed at the old URLs

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

[Unreleased]: https://github.com/peakflames/team-city-mcp/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/peakflames/team-city-mcp/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/peakflames/team-city-mcp/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/peakflames/team-city-mcp/releases/tag/v0.1.0
