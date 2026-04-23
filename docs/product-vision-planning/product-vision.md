# TeamCityMcpServers — Product Vision & Brief

**Document Version:** 1.0
**Date:** 2026-04-22
**Status:** Draft

---

## 1. Product Name

**TeamCityMcpServers** — *MCP servers for AI-powered TeamCity interaction*

## 2. Problem Statement

TeamCity is a powerful CI/CD server, but interacting with it programmatically requires navigating a complex REST API with locators, field selectors, and paginated responses. AI assistants (Claude Code, Cline, Cursor, etc.) cannot natively access TeamCity data — users must manually copy-paste build information, test results, and artifact contents into their AI conversations.

The MCP (Model Context Protocol) standard solves this by providing a universal tool interface that any AI client can consume. A TeamCity MCP server would let AI assistants directly query builds, inspect test results, and download artifacts — all through natural language.

No MCP server exists today for TeamCity, leaving a gap in the AI tooling ecosystem. Common operations — checking build status, reviewing test failures, downloading logs — require multiple API calls with complex locator syntax that could be simplified into single MCP tools.

**Current pain points:**

- AI assistants have no way to access TeamCity build data, forcing manual copy-paste workflows
- TeamCity's REST API is powerful but complex — locators, field filtering, and pagination create a steep learning curve
- No MCP server exists for TeamCity, leaving a gap in the AI tooling ecosystem
- Common operations (check build status, get test failures, download logs) require multiple API calls that could be simplified into single MCP tools

## 3. Target Users

| User Group | Primary Need |
|------------|-------------|
| Developers using MCP-compatible AI assistants | Query build status, test results, and artifacts without leaving their AI workflow |
| DevOps / CI engineers | Use AI assistants to investigate build failures, analyze trends, and manage pipelines |
| MCP ecosystem consumers | A ready-to-use TeamCity integration for any MCP client (Claude Code, Cline, Cursor, etc.) |

## 4. Vision Statement

TeamCityMcpServers provides a standardized MCP interface to TeamCity CI/CD operations, enabling any MCP-compatible AI assistant to query builds, trigger pipelines, download artifacts, and retrieve test results — following the proven architecture of the companion Bitbucket MCP server.

## 5. Goals & Success Criteria (MVP/PoC)

| # | Goal | Success Criteria |
|---|------|-----------------|
| 1 | Expose core TeamCity read operations as MCP tools | At least 5 read-oriented tools available and callable from an MCP client |
| 2 | Follow identical architecture to BitbucketMcpServers | Same solution structure: shared tools library + stdio server + HTTP server |
| 3 | Support both stdio and HTTP transports | Both server projects build and serve the same tools |
| 4 | Authenticate via Bearer token | Works with TeamCity access tokens via env var or config |
| 5 | Return results in AI-friendly format | All tools return markdown-formatted output, consistent with Bitbucket MCP patterns |
| 6 | Prove the concept works end-to-end | At least one tool callable from Claude Code or MCP Inspector |

## 6. MVP Scope Summary

**Build Information**

- List recent builds for a build type (status, number, branch, date)
- Get build details (status, duration, agent, triggered by, VCS revisions)
- Get build log / build problems

**Test Results**

- Get test occurrences for a build (pass/fail/muted, duration)

**Artifacts**

- List artifacts for a build
- Download artifact content

**Cross-cutting Concerns**

- Bearer token authentication
- Configurable TeamCity base URL
- Error handling with FluentResults pattern
- Markdown output formatting
- Serilog logging

## 7. Out of Scope for MVP

- Triggering builds (write operation — deferred to post-PoC)
- Creating/modifying build configurations or projects
- Agent management (pools, enable/disable)
- Build queue management
- Build tagging or commenting
- User/permission management
- Build statistics and trends
- VCS root configuration
- OAuth or other auth methods beyond Bearer token
- Multi-server support (single TeamCity instance only)

## 8. Key Business Scenarios

**Scenario 1 — Investigate a build failure:** A developer asks their AI assistant "why did the latest build fail on main?" The assistant uses MCP tools to fetch the latest build for the build type, retrieves build problems and test failures, and summarizes the root cause.

**Scenario 2 — Review test results:** A developer asks "what tests failed in build #4521?" The assistant fetches test occurrences, filters to failures, and presents a summary with test names, durations, and failure details.

**Scenario 3 — Retrieve a build artifact:** A developer asks "get me the build-report.json from the latest successful build." The assistant lists artifacts, downloads the requested file, and presents or analyzes its contents.

**Scenario 4 — Check build status across branches:** A developer asks "what's the build status on the release branch?" The assistant queries recent builds filtered by branch and returns a status summary.

## 9. Design Direction

- Mirror the Bitbucket MCP server's code structure and patterns exactly — same solution layout, naming conventions, and DI patterns
- All tool output in markdown format with XML metadata tags where structured data is needed (matching Bitbucket MCP style)
- Tools should simplify TeamCity's locator syntax — users pass simple parameters, tools construct locators internally
- Consistent error messaging with "ERROR:" prefix pattern
- No UI — tools-only interface consumed by MCP clients

## 10. Data Strategy

- All data is fetched live from TeamCity REST API on each tool call — no caching, no local storage
- Single `HttpClient` per scope with Bearer token in default request headers
- TeamCity base URL and access token configured via environment variables or appsettings
- Pagination handled internally by tools — callers receive complete result sets
- JSON responses deserialized using source-generated `JsonSerializerContext` for AOT compatibility

## 11. Backlog / Future Vision

- **Trigger builds** — queue builds for a specific branch/revision (first write operation)
- **Build queue management** — list queued builds, cancel queued builds
- **Build log streaming** — retrieve and search build step logs
- **Build comparison** — diff two builds' test results or parameters
- **Project/build type browsing** — list projects, list build types within a project
- **Agent status** — list agents, check pool availability
- **Build tagging and commenting** — annotate builds via MCP
- **Build statistics and trends** — aggregate pass/fail rates over time
- **Multi-server support** — connect to multiple TeamCity instances
- **VCS root inspection** — query repository and branch mappings
