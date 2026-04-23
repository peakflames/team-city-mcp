# Epic 8BeeXFT: Build Query Tools

**Phase:** 2 — Core Tools
**Status:** Not Started
**Dependencies:** Epic rPn7Wbh (TeamCity Client & Auth)

---

## Description

Implement the MCP tools for querying TeamCity builds — listing recent builds with filters and getting detailed build information. These are the most commonly needed tools for AI-assisted CI/CD workflows, covering build status checks and failure investigation.

## At Completion, a User Can

- Ask an AI assistant to list recent builds for a build type
- Filter builds by branch name or build status
- Get detailed information about a specific build including problems and VCS revisions
- See results formatted as readable markdown tables

## Acceptance Criteria

### Tool Class Setup
- [ ] `BuildTools` partial class with `IServiceProvider` constructor injection, following PullRequestTools pattern (PV 5.2)
- [ ] Partial class files follow naming convention `BuildTools_{ToolName}.cs` (PV 5.2)

### list_builds Tool
- [ ] `list_builds` MCP tool with parameters: `buildTypeId` (required), `branch` (optional), `status` (optional), `count` (optional, default 10) (ConOps S1.2, S4.2)
- [ ] Constructs TeamCity locator string internally from parameters (PV 9)
- [ ] Calls `GET /app/rest/builds?locator=...&fields=build(id,number,status,state,branchName,startDate,finishDate,webUrl)` (ConOps S1.2)
- [ ] Returns markdown table with columns: ID, Number, Status, Branch, Started, Finished, Web URL (ConOps S1.3, PV 5.5)
- [ ] Handles pagination if results exceed requested count (PV 10)

### get_build_details Tool
- [ ] `get_build_details` MCP tool with parameter: `buildId` (required) (ConOps S1.4)
- [ ] Calls `GET /app/rest/builds/id:{buildId}` with fields for status, agent, triggered info, revisions, and build problems (ConOps S1.4)
- [ ] Returns markdown document with build metadata in XML tags and human-readable sections (PV 5.5)
- [ ] Includes build problems section if any exist (ConOps S1.4)
- [ ] Includes VCS revision information (commit hash, branch) (ConOps S1.4)

### JSON Models
- [ ] Source-generated `JsonSerializerContext` for TeamCity build API responses (PV 10)

## Verification

- [ ] `dotnet build TeamCityMcpServers.sln` succeeds
- [ ] `list_builds` returns a formatted markdown table for a valid build type ID
- [ ] `list_builds` with branch filter returns only builds on that branch
- [ ] `get_build_details` returns comprehensive build info including problems for a failed build
- [ ] Both tools return "ERROR:" prefixed messages for invalid inputs

## Key Components

- `src/TeamCityMcpTools/BuildTools.cs` — partial class constructor
- `src/TeamCityMcpTools/BuildTools_ListBuilds.cs` — list_builds tool
- `src/TeamCityMcpTools/BuildTools_GetBuildDetails.cs` — get_build_details tool
- `src/TeamCityMcpTools/Models/BuildModels.cs` — JSON response models
- `src/TeamCityMcpTools/Models/TeamCityJsonContext.cs` — source-generated JSON context
