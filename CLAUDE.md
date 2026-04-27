# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP (Model Context Protocol) servers for interacting with TeamCity CI/CD. Provides two deployment options:
- **TeamCityMcpServer**: Console app using stdio transport for local MCP clients (Claude Code, Cline, etc.)
- **TeamCityRemoteMcpServer**: ASP.NET Web API using HTTP transport for remote/containerized deployment

## Build Commands

Use the Python build automation script for all build and run operations:

```bash
python build.py build    # Build solution
python build.py start    # Start HTTP server in background (port 8080)
python build.py stop     # Stop background HTTP server
python build.py status   # Check if HTTP server is running
python build.py run      # Run stdio server in foreground (blocks terminal)
```

### Alternative dotnet commands
```bash
dotnet build TeamCityMcpServers.sln
dotnet publish src/TeamCityMcpServer/TeamCityMcpServer.csproj -o publish
dotnet run --project src/TeamCityRemoteMcpServer/TeamCityRemoteMcpServer.csproj
dotnet publish src/TeamCityRemoteMcpServer/TeamCityRemoteMcpServer.csproj /t:PublishContainer -r linux-x64
```

### Prerequisites
```bash
pip install psutil
```

### URLs (when running)
- http://localhost:8080/mcp - MCP streamable HTTP endpoint
- http://localhost:8080/sse - MCP SSE endpoint

## Architecture

```
src/
├── TeamCityMcpServer/        # Console app (stdio MCP transport)
├── TeamCityRemoteMcpServer/  # ASP.NET app (HTTP/SSE MCP transport)
└── TeamCityMcpTools/         # Shared library with MCP tools and TeamCity client
```

### Key Patterns

**MCP Tools**: Defined in `TeamCityMcpTools/BuildTools*.cs` and `TeamCityMcpTools/ProjectTools*.cs` as partial classes. Each MCP tool is a method with `[McpServerTool]` attribute. Add new tools in separate partial class files following the naming convention `BuildTools_<ToolName>.cs` or `ProjectTools_<ToolName>.cs`.

**Client Factory Pattern**: `ITeamCityClientFactory` creates `TeamCityClient` instances. Two implementations:
- `TeamCityClientFactory`: For stdio server, resolves credentials from CLI args or env vars
- `TeamCityRemoteClientFactory`: For HTTP server, resolves credentials from appsettings with env var override

**Configuration**:
- Stdio server: CLI args (`-u/--url`, `-t/--token`) or env vars (`TEAM_CITY_URL`, `TEAM_CITY_ACCESS_TOKEN`)
- Remote server: `appsettings.json` with `TeamCityConfig` section; env vars `TEAM_CITY_URL` and `TEAM_CITY_ACCESS_TOKEN` take precedence

### Dependencies

- **FluentResults**: Result pattern for error handling
- **ModelContextProtocol**: MCP SDK (.NET)
- **Serilog**: Logging

## Adding New MCP Tools

1. Decide which class the tool belongs to: `BuildTools` (build/run operations) or `ProjectTools` (project/config navigation)
2. Create a new file: `src/TeamCityMcpTools/BuildTools_YourToolName.cs` (or `ProjectTools_YourToolName.cs`)
3. Place using statements in `GlobalUsings.cs`, not in individual files
4. Use this template:

```csharp
namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_your_tool_name"),
     Description("Description of your tool")]
    public async Task<string> TeamCityYourToolName(
        [Description("Description of parameter")] string parameterName)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();

        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            // Call TeamCity REST API via client.HttpClient
            var response = await client.HttpClient.GetAsync("app/rest/...");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            // Deserialize and return results in markdown format
            return "Your result in markdown format";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed due to exception '{ex.Message}'";
        }
    }
}
```

## C# Coding Conventions

- Use `var` for all variables
- Use curly braces for all blocks
- Prefer Global Using Statements over Local Using Statements (add to `GlobalUsings.cs`)
- Prefer FluentResults over null handling or Exceptions for error handling
- Return error messages with "ERROR:" prefix
- Return results in markdown format
- All tool names use `teamcity_` prefix in snake_case (e.g., `teamcity_list_builds`)

### MCP Tool Attribute Syntax
Use multi-line format — Description must be a separate attribute:
```csharp
[McpServerTool(Name = "teamcity_tool_name"),
 Description("Tool description")]
```

## Verification Before Commit

A successful build does NOT equal working code. The workflow should be:

1. Implement changes
2. Build: `python build.py build`
3. Start: `python build.py start`
4. Verify: Use MCP tools or manual testing against a real TeamCity instance
5. Commit only after verification

## Git Workflow

- Feature branches target `develop`, not `main`
- Prefer `--no-ff` when merging to preserve commit history
- Use explicit file paths in `git add` commands rather than wildcards

## CRITICAL: appsettings.json Security

**NEVER commit `src/TeamCityRemoteMcpServer/appsettings*.json`** — it may contain sensitive credentials.

- Never use `git add` on this file
- Never stage, reset, or checkout this file
- Use explicit file paths in git commands to avoid accidentally including it
- Always use environment variables (`TEAM_CITY_URL`, `TEAM_CITY_ACCESS_TOKEN`) for credentials

## Debugging

Debug the streamable HTTP server using MCP Inspector:
1. Start `TeamCityRemoteMcpServer` (`python build.py start`)
2. Run `npx @modelcontextprotocol/inspector`
3. Connect with TransportType: streamable http, URL: http://localhost:8080/mcp

## Version Management

Update version and container tag in `src/TeamCityRemoteMcpServer/TeamCityRemoteMcpServer.csproj` — the `Version` and `ContainerImageTag` properties — before publishing.

## Release Protocol

When the user requests "perform a release":

1. **Update CHANGELOG.md** — Change "Unreleased" to today's date, ensure all changes are documented
2. **Commit and push develop** — Add explicit files, commit with "Release version X.Y.Z" message, push
3. **Merge to main** — `git checkout main && git pull && git merge develop --no-ff` with merge commit message, push
4. **Tag and push** — `git tag -a vX.Y.Z -m "Release version X.Y.Z"`, push tag
5. **Prepare next version** — Switch to develop, bump versions in `TeamCityRemoteMcpServer.csproj` (`Version` and `ContainerImageTag`), add "Unreleased" section to CHANGELOG.md, commit "prepare for next development cycle (X.Y.Z+1)", push

Important: Use `--no-ff` for merges, explicit file paths in `git add`, never commit `appsettings*.json`
