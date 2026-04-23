# Epic rPn7Wbh: TeamCity Client & Auth

**Phase:** 1 — Foundation
**Status:** Not Started
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
- [ ] `TeamCityClient` class wraps HttpClient with Bearer token auth and configurable base URL (PV 5.4, PV 10)
- [ ] `ConnectAsync()` method validates connectivity by calling `GET /app/rest/server` (ConOps S6)
- [ ] All HTTP requests include `Authorization: Bearer {token}` and `Accept: application/json` headers (PV 5.4)
- [ ] Client returns `FluentResults.Result` types for error handling (PV 9)

### Factory Pattern
- [ ] `ITeamCityClientFactory` interface with `Task<Result<TeamCityClient>> CreateClientAsync()` method (PV 5.2)
- [ ] `TeamCityClientFactory` implementation for stdio server — reads config from CLI args or env vars (PV 5.4)
- [ ] `TeamCityRemoteClientFactory` implementation for HTTP server — reads config from appsettings + env vars (PV 5.4)

### Configuration
- [ ] `TeamCityConfig` record for stdio server config (serverUrl, accessToken) (PV 5.4)
- [ ] Environment variable support: `TEAMCITY_URL`, `TEAMCITY_ACCESS_TOKEN` (PV 5.4)

## Verification

- [ ] `dotnet build TeamCityMcpServers.sln` succeeds
- [ ] `TeamCityClient.ConnectAsync()` returns success when pointed at a valid TeamCity instance
- [ ] `TeamCityClient.ConnectAsync()` returns a clear error when the token is invalid or server is unreachable
- [ ] Factory creates client with correct auth headers

## Key Components

- `src/TeamCityMcpTools/TeamCityClient.cs` — HTTP client wrapper
- `src/TeamCityMcpTools/ITeamCityClientFactory.cs` — factory interface
- `src/TeamCityMcpTools/TeamCityClientFactory.cs` — stdio factory implementation
- `src/TeamCityMcpTools/TeamCityConfig.cs` — configuration record
- `src/TeamCityRemoteMcpServer/TeamCityRemoteClientFactory.cs` — HTTP server factory
