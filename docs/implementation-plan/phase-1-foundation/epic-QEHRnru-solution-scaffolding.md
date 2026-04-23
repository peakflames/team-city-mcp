# Epic QEHRnru: Solution Scaffolding

**Phase:** 1 — Foundation
**Status:** Not Started
**Dependencies:** None

---

## Description

Create the .NET solution structure mirroring the BitbucketMcpServers architecture — three projects (shared tools library, stdio server, HTTP server) with build automation, logging, and global usings. This establishes the foundation that all subsequent epics build on.

## At Completion, a User Can

- Clone the repo and build the entire solution with `dotnet build`
- See three projects in the solution matching the Bitbucket MCP structure
- Run `python build.py build` to build the solution
- See Serilog logging configured in both server projects

## Acceptance Criteria

### Solution Structure
- [ ] Solution file `TeamCityMcpServers.sln` exists at repo root with three projects (PV 5.2)
- [ ] `src/TeamCityMcpServer/` console app project targeting net9.0 (PV 5.2)
- [ ] `src/TeamCityRemoteMcpServer/` ASP.NET web app project targeting net9.0 (PV 5.2)
- [ ] `src/TeamCityMcpTools/` class library project targeting net9.0, referenced by both server projects (PV 5.2)

### Build & Tooling
- [ ] `build.py` script supports `build`, `run`, `start`, `stop`, `status` commands (PV 5.2, PV 9)
- [ ] `dotnet build TeamCityMcpServers.sln` completes without errors (PV 5.2)

### Shared Configuration
- [ ] `GlobalUsings.cs` in TeamCityMcpTools with common imports: FluentResults, ModelContextProtocol.Server, System.ComponentModel, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, System.Text.Json (PV 9)
- [ ] NuGet dependencies added: FluentResults, ModelContextProtocol, Serilog packages (PV 9)

## Verification

- [ ] `dotnet build TeamCityMcpServers.sln` succeeds with zero warnings
- [ ] `python build.py build` succeeds
- [ ] Solution structure matches: `src/TeamCityMcpServer/`, `src/TeamCityRemoteMcpServer/`, `src/TeamCityMcpTools/`

## Key Components

- `TeamCityMcpServers.sln` — solution file
- `src/TeamCityMcpTools/TeamCityMcpTools.csproj` — shared library project
- `src/TeamCityMcpTools/GlobalUsings.cs` — global using statements
- `src/TeamCityMcpServer/TeamCityMcpServer.csproj` — stdio server project
- `src/TeamCityRemoteMcpServer/TeamCityRemoteMcpServer.csproj` — HTTP server project
- `build.py` — build automation script
