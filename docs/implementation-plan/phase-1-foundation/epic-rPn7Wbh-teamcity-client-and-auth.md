# Epic rPn7Wbh: TeamCity Client & Auth

**Phase:** 1 — Foundation
**Status:** Implemented
**Dependencies:** Epic QEHRnru (Solution Scaffolding)

---

## Description

Implement the TeamCity HTTP client with Bearer token authentication and the client factory pattern matching the Bitbucket MCP architecture. This provides the authenticated HTTP layer that all MCP tools will use to call the TeamCity REST API.

## At Completion, a User Can

- Configure TeamCity server URL and access token via environment variables or appsettings
- Have the client factory create authenticated HttpClient instances for any tool
- See clear error messages when authentication fails or the server is unreachable

## Acceptance Criteria

### Client Implementation
- [x] `TeamCityClient` class wraps HttpClient with Bearer token auth and configurable base URL (PV 5.4, PV 10)
- [x] `ConnectAsync()` method validates connectivity by calling `GET /app/rest/server` (ConOps S6)
- [x] All HTTP requests include `Authorization: Bearer {token}` and `Accept: application/json` headers (PV 5.4)
- [x] Client returns `FluentResults.Result` types for error handling (PV 9)

### Factory Pattern
- [x] `ITeamCityClientFactory` interface with `Task<Result<TeamCityClient>> CreateClientAsync()` method (PV 5.2)
- [x] `TeamCityClientFactory` implementation for stdio server — reads config from CLI args or env vars (PV 5.4)
- [x] `TeamCityRemoteClientFactory` implementation for HTTP server — reads config from appsettings + env vars (PV 5.4)

### Configuration
- [x] `TeamCityConfig` record for stdio server config (serverUrl, accessToken) (PV 5.4)
- [x] Environment variable support: `TEAM_CITY_URL`, `TEAM_CITY_ACCESS_TOKEN` (PV 5.4)

## Verification

- [x] `dotnet build TeamCityMcpServers.sln` succeeds
- [x] `TeamCityClient.ConnectAsync()` returns success when pointed at a valid TeamCity instance
- [x] `TeamCityClient.ConnectAsync()` returns a clear error when the token is invalid or server is unreachable
- [x] Factory creates client with correct auth headers

## Key Components

- `src/TeamCityMcpTools/TeamCityClient.cs` — HTTP client wrapper
- `src/TeamCityMcpTools/ITeamCityClientFactory.cs` — factory interface
- `src/TeamCityMcpTools/TeamCityClientFactory.cs` — stdio factory implementation
- `src/TeamCityMcpTools/TeamCityConfig.cs` — configuration record
- `src/TeamCityRemoteMcpServer/TeamCityRemoteClientFactory.cs` — HTTP server factory
