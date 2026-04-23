# Handoff: Epic 8BeeXFT — Build Query Tools

**Date:** 2026-04-23
**Status:** Implemented
**Branch:** develop

---

## Spec Deviations

| Original Spec | As-Implemented | Reason |
|---------------|----------------|--------|
| No deviations | — | All acceptance criteria delivered as specified |

---

## Implementation Notes

**Key files created:**
- `src/TeamCityMcpTools/BuildTools.cs` — partial class constructor; takes `IServiceProvider` for scoped DI resolution per tool call
- `src/TeamCityMcpTools/BuildTools_ListBuilds.cs` — `list_builds` MCP tool; builds TeamCity locator string from `buildTypeId`, `branch`, `status`, `count`; returns markdown table
- `src/TeamCityMcpTools/BuildTools_GetBuildDetails.cs` — `get_build_details` MCP tool; fetches build with agent, triggered, revisions, and problemOccurrences fields; returns markdown document with XML metadata tag
- `src/TeamCityMcpTools/Models/BuildModels.cs` — `BuildListResponse`, `BuildSummary`, `BuildDetails`, `BuildTypeInfo`, `AgentInfo`, `TriggeredInfo`, `TriggeredUser`, `RevisionsWrapper`, `RevisionInfo`, `ProblemOccurrencesWrapper`, `ProblemOccurrence`
- `src/TeamCityMcpTools/Models/TeamCityJsonContext.cs` — `[JsonSerializable]` source-generated context for all model types

**Key files modified:**
- `src/TeamCityMcpTools/GlobalUsings.cs` — added `System.Text.Json`, `System.Text.Json.Serialization`, `TeamCityMcpTools.Models`
- `src/TeamCityMcpServer/Program.cs` — added `.WithTools<BuildTools>()` to MCP server registration
- `src/TeamCityRemoteMcpServer/Program.cs` — added `.WithTools<BuildTools>()` to MCP server registration

**Notable design decisions:**
- `FormatTcDate()` helper parses TeamCity's `20241119T102304+0000` format into `YYYY-MM-DD HH:MM` for readable output
- `list_builds` always includes `state:finished` in the locator to avoid returning running builds in the table
- `get_build_details` requests `problemOccurrences(count,problemOccurrence(...))` — the nested `count` field is used to gate whether to render the "Build Problems" section

---

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build TeamCityMcpServers.sln` — zero errors, zero warnings | PASS |
| `list_builds` returns markdown table for valid build type ID | PASS |
| `list_builds` with branch filter returns only matching branch builds | PASS |
| `get_build_details` returns full build info including VCS revisions | PASS |
| `get_build_details` includes Build Problems section for failed builds | PASS |
| Both tools return `ERROR:` prefixed messages for invalid inputs | PASS |
