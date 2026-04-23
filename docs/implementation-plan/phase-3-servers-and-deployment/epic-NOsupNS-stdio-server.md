# Epic NOsupNS: Stdio Server

**Phase:** 3 — Servers & Deployment
**Status:** Not Started
**Dependencies:** Epic 8BeeXFT (Build Query Tools), Epic uQKcsyL (Test Results & Artifact Tools)

---

## Description

Wire up the stdio-transport MCP server (console app) with DI registration, CLI argument parsing, and Serilog logging. This enables local MCP clients like Claude Code and Cline to use the TeamCity tools via stdin/stdout.

## At Completion, a User Can

- Run the console app with CLI arguments or environment variables for TeamCity config
- Connect an MCP client (Claude Code, Cline) via stdio transport
- Call all TeamCity MCP tools through their AI assistant
- See logs written to a rolling file

## Acceptance Criteria

### Program.cs Setup
- [ ] CLI argument parsing: `-s`/`--server` (TeamCity URL), `-t`/`--token` (access token) (PV 5.4)
- [ ] Environment variable fallback: `TEAMCITY_URL`, `TEAMCITY_ACCESS_TOKEN` (PV 5.4)
- [ ] DI registration: `TeamCityConfig` singleton, `ITeamCityClientFactory` as scoped `TeamCityClientFactory` (PV 5.2)
- [ ] MCP server registration with stdio transport and `BuildTools` (PV 5.3)
- [ ] Serilog configured with rolling file and console sinks (PV 9)
- [ ] Exit code 1 with clear message when required config is missing (PV 5.5)

### Project Configuration
- [ ] `TeamCityMcpServer.csproj` configured as Exe, single file, self-contained, trimmed (PV 5.2)

## Verification

- [ ] `dotnet build TeamCityMcpServers.sln` succeeds
- [ ] `dotnet run --project src/TeamCityMcpServer -- -s https://teamcity.example.com -t test-token` starts without error
- [ ] MCP Inspector can connect via stdio and list all 5 tools
- [ ] Calling a tool through MCP Inspector returns expected results
- [ ] Missing config produces exit code 1 with helpful error message

## Key Components

- `src/TeamCityMcpServer/Program.cs` — entry point with DI and CLI parsing
- `src/TeamCityMcpServer/TeamCityMcpServer.csproj` — project file
