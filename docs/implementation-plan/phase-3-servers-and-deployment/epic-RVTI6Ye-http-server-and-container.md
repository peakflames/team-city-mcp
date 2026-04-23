# Epic RVTI6Ye: HTTP Server & Container

**Phase:** 3 — Servers & Deployment
**Status:** Not Started
**Dependencies:** Epic 8BeeXFT (Build Query Tools), Epic uQKcsyL (Test Results & Artifact Tools)

---

## Description

Wire up the HTTP-transport MCP server (ASP.NET app) with streamable HTTP/SSE endpoints, DI registration, container support, and Serilog logging. This enables remote MCP clients and containerized deployment of the TeamCity MCP server.

## At Completion, a User Can

- Run the HTTP server locally and connect via MCP Inspector
- Connect MCP clients via HTTP/SSE transport
- Build a Docker container for the server
- Configure TeamCity credentials via appsettings.json and environment variables

## Acceptance Criteria

### Program.cs Setup
- [ ] Configuration from `appsettings.json` with `TeamCityConfig` section for server URL (PV 5.4)
- [ ] Environment variable override: `TEAMCITY_MCP_URL`, `TEAMCITY_MCP_ACCESS_TOKEN` (PV 5.4)
- [ ] DI registration: `TeamCityProjectConfig` singleton, `ITeamCityClientFactory` as scoped `TeamCityRemoteClientFactory`, `IHttpContextAccessor` (PV 5.2)
- [ ] MCP server registration with HTTP transport and `BuildTools` (PV 5.3)
- [ ] Serilog configured with rolling file, debug, and console sinks (PV 9)
- [ ] `MapMcp()` at root and `/mcp` path for client compatibility (PV 5.3)

### Container Support
- [ ] `TeamCityRemoteMcpServer.csproj` configured with `EnableSdkContainerSupport`, `ContainerRepository`, `ContainerImageTag` (PV 5.2)
- [ ] `dotnet publish /t:PublishContainer` produces a working container image (PV 5.2)

## Verification

- [ ] `dotnet build TeamCityMcpServers.sln` succeeds
- [ ] `python build.py start` launches the HTTP server on configured port
- [ ] MCP Inspector connects via streamable HTTP and lists all 5 tools
- [ ] Calling a tool through MCP Inspector returns expected results
- [ ] `python build.py mcp tools` lists all tools via the running server
- [ ] `python build.py mcp ping` confirms connectivity

## Key Components

- `src/TeamCityRemoteMcpServer/Program.cs` — entry point with DI and endpoint mapping
- `src/TeamCityRemoteMcpServer/TeamCityRemoteMcpServer.csproj` — project file with container config
- `src/TeamCityRemoteMcpServer/TeamCityRemoteClientFactory.cs` — HTTP server client factory
- `src/TeamCityRemoteMcpServer/appsettings.json` — base configuration
