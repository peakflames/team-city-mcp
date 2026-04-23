# Epic uQKcsyL: Test Results & Artifact Tools

**Phase:** 2 — Core Tools
**Status:** Not Started
**Dependencies:** Epic rPn7Wbh (TeamCity Client & Auth)

---

## Description

Implement MCP tools for retrieving test results from builds and browsing/downloading build artifacts. These tools complete the read-only PoC scope, enabling AI assistants to investigate test failures and access build outputs.

## At Completion, a User Can

- Ask an AI assistant what tests failed in a specific build
- See test results with pass/fail status, duration, and names
- List all artifacts produced by a build
- Download the content of a specific artifact through the AI assistant

## Acceptance Criteria

### get_build_test_results Tool
- [ ] `get_build_test_results` MCP tool with parameters: `buildId` (required), `status` (optional filter: "FAILURE", "SUCCESS", "UNKNOWN") (ConOps S2.2)
- [ ] Calls `GET /app/rest/builds/id:{buildId}/testOccurrences?fields=testOccurrence(id,name,status,duration,muted,currentlyMuted)` (ConOps S2.2)
- [ ] Returns markdown table with columns: Name, Status, Duration, Muted (ConOps S2.3, PV 5.5)
- [ ] Handles pagination for builds with many tests (PV 10)
- [ ] Returns summary line with total/passed/failed/muted counts (ConOps S2.3)

### list_build_artifacts Tool
- [ ] `list_build_artifacts` MCP tool with parameter: `buildId` (required) (ConOps S3.3)
- [ ] Calls `GET /app/rest/builds/id:{buildId}/artifacts/children` (ConOps S3.3)
- [ ] Returns markdown table with columns: Name, Size, Modified (ConOps S3.3, PV 5.5)
- [ ] Handles nested directory structures in artifact listing (ConOps S3.3)

### download_build_artifact Tool
- [ ] `download_build_artifact` MCP tool with parameters: `buildId` (required), `artifactPath` (required) (ConOps S3.4)
- [ ] Calls `GET /app/rest/builds/id:{buildId}/artifacts/content/{artifactPath}` (ConOps S3.4)
- [ ] Returns artifact content as string (PV 5.5)
- [ ] Returns clear error for artifacts that are too large or binary (ConOps S8 — artifact size constraint)

### JSON Models
- [ ] Source-generated JSON models for test occurrence and artifact API responses (PV 10)

## Verification

- [ ] `dotnet build TeamCityMcpServers.sln` succeeds
- [ ] `get_build_test_results` returns a formatted table for a build with tests
- [ ] `get_build_test_results` with status filter returns only matching tests
- [ ] `list_build_artifacts` shows artifact tree for a build with artifacts
- [ ] `download_build_artifact` returns content of a text/JSON artifact
- [ ] All tools return "ERROR:" prefixed messages for invalid build IDs

## Key Components

- `src/TeamCityMcpTools/BuildTools_GetBuildTestResults.cs` — test results tool
- `src/TeamCityMcpTools/BuildTools_ListBuildArtifacts.cs` — artifact listing tool
- `src/TeamCityMcpTools/BuildTools_DownloadBuildArtifact.cs` — artifact download tool
- `src/TeamCityMcpTools/Models/TestOccurrenceModels.cs` — test result JSON models
- `src/TeamCityMcpTools/Models/ArtifactModels.cs` — artifact JSON models
