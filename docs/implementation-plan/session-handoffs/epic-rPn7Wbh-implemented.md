# Handoff: Epic rPn7Wbh — TeamCity Client & Auth

**Date:** 2026-04-23
**Status:** Implemented
**Branch:** develop

---

## Spec Deviations

| Original Spec | As-Implemented | Reason |
|---------------|----------------|--------|
| Env var `TEAMCITY_URL` | `TEAM_CITY_URL` | Matches actual env vars already configured in the local/machine environment |
| Env var `TEAMCITY_ACCESS_TOKEN` | `TEAM_CITY_ACCESS_TOKEN` | Same reason as above |
| `TeamCityConfig` described as "stdio server config" | `TeamCityConfig` is shared by both stdio and remote factories | Single record is cleaner; both factories need the same two fields |

---

## Implementation Notes

**Key files created:**
- `src/TeamCityMcpTools/TeamCityConfig.cs` — `record TeamCityConfig(string ServerUrl, string AccessToken)`
- `src/TeamCityMcpTools/TeamCityClient.cs` — raw `HttpClient` wrapper; `Authorization: Bearer` + `Accept: application/json` default headers; `ConnectAsync()` calls `GET /app/rest/server`; returns `Result`/`Result<T>` throughout; exposes `HttpClient` property for future tools
- `src/TeamCityMcpTools/ITeamCityClientFactory.cs` — `Task<Result<TeamCityClient>> CreateClientAsync()`
- `src/TeamCityMcpTools/TeamCityClientFactory.cs` — stdio factory; primary constructor takes `TeamCityConfig`
- `src/TeamCityRemoteMcpServer/TeamCityRemoteClientFactory.cs` — HTTP server factory; constructor takes `TeamCityConfig` + `ILogger<T>`
- `src/TeamCityMcpServer/Program.cs` — full Serilog bootstrap + `GetConfigValue()` helper (CLI args → env vars); DI wiring; `WithStdioServerTransport()`
- `src/TeamCityRemoteMcpServer/Program.cs` — full Serilog bootstrap; env var + appsettings config resolution; DI wiring; `WithHttpTransport()`; SSE workaround middleware; `app.MapMcp()` + `app.MapMcp("mcp")`
- `src/TeamCityRemoteMcpServer/appsettings.json` — added `TeamCityConfig.ServerUrl` fallback key

**Packages added:**
- `TeamCityMcpServer`: `Serilog.Sinks.File 7.0.0`, `Serilog.Sinks.Debug 3.0.0`, `Microsoft.Extensions.Hosting`
- `TeamCityRemoteMcpServer`: `ModelContextProtocol 1.2.0`, `ModelContextProtocol.AspNetCore 1.2.0`

**Deleted:**
- `src/TeamCityMcpTools/Class1.cs` — placeholder stub

**Additional discovery:** `ModelContextProtocol.AspNetCore` must be referenced directly in the web server project — it does not flow properly as a transitive dependency from `TeamCityMcpTools`. Both `WithHttpTransport()` and `app.MapMcp()` live in this package.

---

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build TeamCityMcpServers.sln` — zero errors, zero warnings | PASS |
| `GET /app/rest/server` with valid token → HTTP 200 | PASS |
| `GET /app/rest/server` with bad token → HTTP 401 | PASS |
| `GET` against bogus hostname → connection failure | PASS |
