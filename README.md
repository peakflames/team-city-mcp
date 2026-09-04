# TeamCity MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for TeamCity CI/CD integration.

## Available MCP Tools

The server exposes the following tools for navigating TeamCity projects, inspecting build configurations, and querying build state.

### Project & Build Configuration Tools

| Tool | Description |
|------|-------------|
| `teamcity_list_projects` | Lists projects with parent relationships; optional name/id regex filters. |
| `teamcity_get_project` | Project details by ID — child projects, build configurations, and templates. |
| `teamcity_get_project_hierarchy` | Renders the project tree as an indented markdown list; optional root and depth. |
| `teamcity_get_project_parameters` | Configuration parameters defined on a project (cascade to child build configs); inheritance/name filters. |
| `teamcity_get_project_features` | Project features — report tabs, graphs, versioned settings, issue trackers, etc. |
| `teamcity_list_build_types` | Lists build configurations (build types); optional project scope and name/id regex filters. |
| `teamcity_get_build_type` | Full build config detail — template linkage, settings, VCS roots, triggers, steps, agent reqs, dependencies, build features. |
| `teamcity_get_build_type_parameters` | Parameters defined on a build configuration (own/inherited/all); name filter. |
| `teamcity_get_build_type_features` | Build features on a build configuration (Matrix Build, failure conditions, swabra, etc.), grouped by type; own/inherited. |
| `teamcity_get_build_type_dependency_graph` | Design-time snapshot/artifact dependency graph for a build type (dependencies/dependents/both); markdown or mermaid. |
| `teamcity_list_templates` | Lists build templates; optional project scope and name/id regex filters. |
| `teamcity_list_vcs_roots` | Lists VCS roots (id and name); optional project scope and name filter. |
| `teamcity_get_vcs_root` | Full connection detail for one VCS root (URL, branch spec, auth); secure props redacted. |
| `teamcity_get_audit_log` | Audit log entries (config/permission changes); optional build-type/project scope and config-changes-only filter. |

### Build Tools

| Tool | Description |
|------|-------------|
| `teamcity_list_builds` | Lists recent builds for a build type; filters for project, branch, status, state, count. |
| `teamcity_get_build` | Comprehensive build details — status, agent, VCS revisions, build problems, composite flag. |
| `teamcity_get_build_status` | Compact single-line status (state, branch, progress, URL). |
| `teamcity_get_running_builds` | All running builds, optionally filtered by project. |
| `teamcity_get_queued_builds` | All builds in the queue, optionally filtered by project. |
| `teamcity_search_builds` | Searches builds across projects by multiple criteria (project, build type, branch, status, state, agent, tags, dates). |
| `teamcity_get_build_changes` | VCS changes (commits) included in a build — author, comment, changed files. |
| `teamcity_get_build_problems` | Dedicated build problem occurrences (exit-code, OOM, dependency failures) — distinct from test failures. |
| `teamcity_get_build_dependency_tree` | Walks a build's actual snapshot/artifact dependency chain; per-node status; up/down direction; markdown or mermaid. |
| `teamcity_get_build_parameters` | Resulting parameters actually applied to a build after overrides; name filter. |
| `teamcity_get_build_tests` | Test occurrences for a build — accurate summary counts and per-test failure detail (failed/muted/all); composite/matrix-aware with per-sub-build totals. |
| `teamcity_get_test_history` | Follows one test by name across builds; useful for flakiness or regression onset. |
| `teamcity_get_build_log_failures` | For failed tests, returns console-log context around each failure by searching the build log. |
| `teamcity_search_build_log` | Searches a build's full console log by regex/literal with surrounding context. |
| `teamcity_list_build_artifacts` | Lists artifact files/dirs (including hidden `.teamcity/...`); optional subdirectory navigation. |
| `teamcity_get_build_artifact_content` | Text content of a single artifact file; refuses binaries and truncates large files. |
| `teamcity_list_mutes` | Mute details — reason, who/when, scope (project/build config), and resolution policy. |
| `teamcity_server_info` | TeamCity server version and instance metadata. |

## Projects

- **TeamCityRemoteMcpServer**: ASP.NET Web API-based MCP server for server-based installations (Streamable HTTP transport, stateless; optional OAuth 2.1 authentication and per-caller RBAC)
- **TeamCityMcpServer**: Console-based MCP server for local workstation installations (stdio transport)

## Running via Docker & Linux Server (Recommended)

1. From your Linux server, create a directory for your configuration:

   ```bash
   mkdir -p /opt/teamcity-mcp-server
   cd /opt/teamcity-mcp-server
   ```

2. Pull the Docker image:

   ```bash
   docker pull peakflames/teamcity-remote-mcp-server
   ```

3. Run the Docker container:

   ```bash
   docker run -d \
     --name teamcity-mcp-server \
     -p 8080:8080 \
     -e TEAM_CITY_URL="https://your-teamcity-server" \
     -e TEAM_CITY_ACCESS_TOKEN="your_access_token" \
     peakflames/teamcity-remote-mcp-server
   ```

4. The server should now be running. MCP clients will connect using:
   - **Streamable HTTP Transport**: `http://{{your-server-ip}}:8080/mcp`

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `TEAM_CITY_URL` | Full URL of your TeamCity server (e.g. `https://teamcity.example.com`) | Yes |
| `TEAM_CITY_ACCESS_TOKEN` | TeamCity access token for authentication | Yes |

The server will fail to start if either variable is missing. Both are still required when
optional authentication and per-caller RBAC (below) are enabled — the server uses its own token to
query TeamCity permissions on the caller's behalf.

**How to create a TeamCity access token:**
1. Log in to your TeamCity server
2. Click your profile avatar (top right) > **Profile**
3. Go to the **Access Tokens** tab
4. Click **Create access token**
5. Give it a name and set the appropriate permissions
6. Copy the generated token (you won't be able to see it again)

### How It Works

1. **Environment Variable Resolution**: At startup, `Program.cs` reads `TEAM_CITY_URL` and `TEAM_CITY_ACCESS_TOKEN` from environment variables (these take precedence over `appsettings.json`)
2. **Validation**: The server throws at startup if either variable is missing or empty
3. **Tool Invocation**: When an MCP tool is called, the build type/configuration ID is passed as a function argument
4. **Client Creation**: A TeamCity HTTP client is created using Bearer token authentication with the resolved credentials

### Optional: Authentication and Per-Caller Access Control

`TeamCityRemoteMcpServer` can optionally require a bearer token from an external OAuth 2.1
authorization server, validating callers and publishing RFC 9728 protected-resource metadata at
`/.well-known/oauth-protected-resource/mcp`. Layered on top, per-caller RBAC authorizes each tool
call against the *calling* user's own TeamCity permissions instead of the server's shared access
token. Both are off by default and behavior is unchanged unless you configure them — see
[docs/authentication.md](docs/authentication.md) and [docs/rbac.md](docs/rbac.md).

## Configuring MCP Clients

### Claude Code (CLI)

Add the server using the streamable HTTP transport:

```bash
claude mcp add --scope user --transport http teamcity-remote http://{{your-server-ip}}:8080/mcp
```

If the server has [authentication](docs/authentication.md) enabled, the client performs an OAuth
flow on first connect instead of connecting immediately — that's expected, not a broken command.

### Cline Configuration

1. Open Cline's MCP settings UI
2. Click the "Remote Servers" tab
3. Add the following configuration:

   ```json
   {
     "mcpServers": {
       "TeamCity": {
         "autoApprove": [],
         "disabled": false,
         "timeout": 60,
         "url": "http://{{your-server-ip}}:8080/mcp",
         "transportType": "streamableHttp"
       }
     }
   }
   ```

## Troubleshooting

**Server fails to start:**
- Ensure both `TEAM_CITY_URL` and `TEAM_CITY_ACCESS_TOKEN` environment variables are set
- Check docker logs: `docker logs teamcity-mcp-server`
- Logs are also written to `logs/TeamCityRemoteMcpServer_*.log` inside the container

**Authentication errors (401):**
- Verify the access token is valid and has not expired
- Ensure the token has permission to read builds in TeamCity

**401 from `/mcp` and the client never prompts to log in:**
- Confirm the client supports RFC 9728 protected-resource discovery
- `/mcp` is only protected when `McpAuth:Enabled=true` — see [docs/authentication.md](docs/authentication.md)

**Server refuses to start with an `McpAuth` or `Rbac` validation message:**
- These sections are validated at startup; see the validation-error tables in
  [docs/authentication.md](docs/authentication.md#startup-validation-errors) and
  [docs/rbac.md](docs/rbac.md#startup-validation-errors)

**Every tool call is denied, or projects come back empty, with RBAC enabled:**
- The caller's identity did not resolve, or they lack the required TeamCity permission — RBAC
  fails closed by design; see [docs/rbac.md](docs/rbac.md#troubleshooting)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, build instructions, Docker publishing, and code standards.

## License

See [LICENSE](LICENSE) for details.
