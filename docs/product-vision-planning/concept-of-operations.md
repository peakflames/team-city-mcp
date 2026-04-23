# TeamCityMcpServers — Concept of Operations (ConOps)

**Document Version:** 1.0
**Date:** 2026-04-22
**Status:** Draft

*Companion document to [Product Vision & Brief](product-vision.md)*

---

## 1. Purpose & Scope

This document describes the operational concept for TeamCityMcpServers — a set of MCP (Model Context Protocol) servers that expose TeamCity CI/CD operations as tools consumable by AI assistants. The PoC/MVP focuses on read-only operations: querying builds, retrieving test results, and downloading artifacts.

## 2. Current State ("As-Is")

| Current Method | Limitations |
|---------------|-------------|
| TeamCity Web UI | Manual — must leave AI workflow, copy-paste results back |
| Direct REST API calls | Complex locator syntax, pagination handling, field selection — high friction |
| Custom scripts per team/repo | Not reusable by AI assistants, inconsistent patterns, require local setup |
| Embedded service code | Tightly coupled to specific applications, not exposed as MCP tools |

**Core pain points:**

1. Context-switching between AI assistant and TeamCity UI breaks developer flow
2. Build failure investigation requires multiple manual API calls with complex locator syntax
3. No standardized way for AI tools to access TeamCity data
4. Test result analysis requires downloading artifacts and parsing them manually

## 3. Proposed System ("To-Be")

TeamCityMcpServers exposes TeamCity REST API operations as MCP tools consumable by any MCP-compatible AI client. The shared tools library (`TeamCityMcpTools`) wraps the TeamCity REST API using `HttpClient` with Bearer token authentication, returning markdown-formatted results.

Two deployment options serve different use cases: a stdio-transport console app for local MCP clients (Claude Code, Cline), and an HTTP-transport ASP.NET app for remote or containerized deployment. Both share the same tool implementations.

The PoC focuses on read operations — builds, test results, and artifacts — providing enough coverage for the most common AI-assisted CI/CD workflows without risking unintended mutations.

## 4. User Roles & Profiles

| Role | Typical Questions |
|------|------------------|
| Developer | "Did my build pass?" "What tests failed?" "Show me the build log" |
| DevOps Engineer | "What's the latest build on main?" "Download the test report artifact" |
| AI Assistant (automated) | Needs structured build/test data to answer user questions about CI/CD state |

## 5. Operational Scenarios

### Scenario 1: Investigate a Build Failure

**Actor:** Developer using an MCP-compatible AI assistant
**Trigger:** A build fails and the developer wants to understand why
**Goal:** Get a summary of what went wrong without leaving the AI workflow

**Steps:**

1. Developer asks the AI assistant: "Why did the latest build fail?"
2. AI calls `list_builds` with the build type ID, filtered to recent builds
3. AI identifies the failed build from the results
4. AI calls `get_build_details` with the build ID to get status, problems, and metadata
5. AI calls `get_build_test_results` with the build ID to retrieve test failures
6. AI synthesizes the build problems and failed tests into a human-readable summary
7. AI presents the root cause analysis to the developer

**Outcome:** Developer understands the failure without opening TeamCity UI

### Scenario 2: Review Test Results for a Build

**Actor:** Developer investigating test failures
**Trigger:** Developer wants to know which tests failed in a specific build
**Goal:** Get a filtered list of failed tests with details

**Steps:**

1. Developer asks: "What tests failed in build #4521?"
2. AI calls `get_build_test_results` with build ID 4521
3. AI filters results to failed/errored tests
4. AI presents a table of failed tests with name, duration, and status
5. Developer asks follow-up questions; AI uses already-retrieved data to answer

**Outcome:** Developer has a clear picture of test failures for the build

### Scenario 3: Download a Build Artifact

**Actor:** Developer or DevOps engineer needing artifact contents
**Trigger:** User needs a specific file produced by a build
**Goal:** Retrieve artifact content through the AI assistant

**Steps:**

1. Developer asks: "Get me the build-report.json from the latest build of MyApp_Main"
2. AI calls `list_builds` to find the latest successful build for the build type
3. AI calls `list_build_artifacts` with the build ID to see available files
4. AI calls `download_build_artifact` with build ID and artifact path "build-report.json"
5. AI presents or analyzes the artifact content

**Outcome:** Developer has the artifact content in their AI conversation without manual download

### Scenario 4: Check Build Status Across Branches

**Actor:** Developer checking CI health before merging
**Trigger:** Developer wants to verify builds are green on a target branch
**Goal:** Quick status check of recent builds on a specific branch

**Steps:**

1. Developer asks: "What's the build status on the release/1.5 branch?"
2. AI calls `list_builds` with build type ID and branch filter "release/1.5"
3. AI presents a summary table of recent builds with status, number, and date
4. Developer decides whether to proceed with their merge

**Outcome:** Developer knows the CI health of the target branch

## 6. System Interfaces & Data Flows

**Data Sources:**

| Source | Protocol | Auth | Data Provided |
|--------|----------|------|--------------|
| TeamCity REST API | HTTPS | Bearer Token | Builds, test results, artifacts, build types |

**Data Flow:**

```
MCP Client (Claude Code / Cline / Cursor)
    |
    +-- stdio --> TeamCityMcpServer (Console App)
    |                    |
    +-- HTTP ---> TeamCityRemoteMcpServer (ASP.NET)
                         |
                    TeamCityMcpTools (Shared Library)
                         |
                    HttpClient + Bearer Token
                         |
                    TeamCity REST API
                         |
                    TeamCity Server
```

## 7. Functional Summary

| Area | Tool | Description |
|------|------|-------------|
| Builds | `list_builds` | List recent builds for a build type, with optional branch/status filters |
| Builds | `get_build_details` | Get full details for a specific build (status, agent, revisions, problems) |
| Tests | `get_build_test_results` | Get test occurrences for a build (pass/fail/muted, duration) |
| Artifacts | `list_build_artifacts` | List available artifacts for a build |
| Artifacts | `download_build_artifact` | Download content of a specific artifact by path |

## 8. Operational Constraints & Assumptions

| Constraint | Detail |
|------------|--------|
| Authentication | Bearer token only (TeamCity access tokens) |
| TeamCity version | Assumes modern TeamCity with REST API v2018+ |
| Single instance | One TeamCity server per deployment |
| Read-only MVP | No write/mutation operations in PoC |
| Network access | Server must have HTTPS access to TeamCity instance |
| .NET version | net9.0 (matching Bitbucket MCP server) |
| MCP SDK | ModelContextProtocol 0.7.0-preview.1 (matching Bitbucket MCP) |
| No caching | All data fetched live per request |
| Artifact size | Large binary artifacts may not be practical to return as MCP tool results |

## 9. Glossary

| Term | Definition |
|------|-----------|
| Build Type | A TeamCity build configuration — defines how to build, what to trigger on, and what artifacts to produce |
| Build | A single execution of a build type — has a unique ID, number, status, and associated artifacts |
| Locator | TeamCity's query syntax for filtering API results (e.g., `buildType:id:MyBuild,state:finished,count:10`) |
| Artifact | A file produced by a build and stored in TeamCity (logs, test reports, binaries, etc.) |
| Test Occurrence | A single test execution within a build — has a name, status (pass/fail/muted), and duration |
| Build Problem | An issue detected during a build that caused or contributed to failure |
| Agent | A machine that executes TeamCity builds |
| MCP | Model Context Protocol — standard for exposing tools to AI assistants |
| Stdio transport | MCP communication via stdin/stdout — used for local CLI-based AI clients |
| HTTP transport | MCP communication via HTTP/SSE — used for remote or containerized deployment |
| Bearer Token | TeamCity authentication method using a personal access token in the Authorization header |
