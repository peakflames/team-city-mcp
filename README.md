# TeamCity MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for TeamCity CI/CD integration.

MCP Tools are available for TeamCity operations, including:

- `list_builds`: Gets builds for a given build type/configuration.
- `get_build_details`: Gets detailed information about a specific build.

## Projects

- **TeamCityRemoteMcpServer**: ASP.NET Web API-based MCP server for server-based installations (Streamable HTTP or SSE transport)
- **TeamCityMcpServer**: Console-based MCP server for local workstation installations (stdio transport)

## Running via Docker & Linux Server (Recommended)

1. From your Linux server, create a directory for your configuration:

   ```bash
   mkdir -p /opt/teamcity-mcp-server
   cd /opt/teamcity-mcp-server
   ```

2. Pull the Docker image:

   ```bash
   docker pull peakflames/teamcity-remote-mcp-server
   ```

3. Run the Docker container:

   ```bash
   docker run -d \
     --name teamcity-mcp-server \
     -p 8080:8080 \
     -e TEAM_CITY_URL="https://your-teamcity-server" \
     -e TEAM_CITY_ACCESS_TOKEN="your_access_token" \
     peakflames/teamcity-remote-mcp-server
   ```

4. The server should now be running. MCP clients will connect using:
   - **Streamable HTTP Transport**: `http://{{your-server-ip}}:8080/mcp`
   - **SSE Transport**: `http://{{your-server-ip}}:8080/sse`

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `TEAM_CITY_URL` | Full URL of your TeamCity server (e.g. `https://teamcity.example.com`) | Yes |
| `TEAM_CITY_ACCESS_TOKEN` | TeamCity access token for authentication | Yes |

The server will fail to start if either variable is missing.

**How to create a TeamCity access token:**
1. Log in to your TeamCity server
2. Click your profile avatar (top right) > **Profile**
3. Go to the **Access Tokens** tab
4. Click **Create access token**
5. Give it a name and set the appropriate permissions
6. Copy the generated token (you won't be able to see it again)

### How It Works

1. **Environment Variable Resolution**: At startup, `Program.cs` reads `TEAM_CITY_URL` and `TEAM_CITY_ACCESS_TOKEN` from environment variables (these take precedence over `appsettings.json`)
2. **Validation**: The server throws at startup if either variable is missing or empty
3. **Tool Invocation**: When an MCP tool is called, the build type/configuration ID is passed as a function argument
4. **Client Creation**: A TeamCity HTTP client is created using Bearer token authentication with the resolved credentials

## Configuring MCP Clients

### Claude Code (CLI)

Add the server using the streamable HTTP transport:

```bash
claude mcp add --scope user --transport http teamcity-remote http://{{your-server-ip}}:8080/mcp
```

### Cline Configuration

1. Open Cline's MCP settings UI
2. Click the "Remote Servers" tab
3. Add the following configuration:

   ```json
   {
     "mcpServers": {
       "TeamCity": {
         "autoApprove": [],
         "disabled": false,
         "timeout": 60,
         "url": "http://{{your-server-ip}}:8080/sse",
         "transportType": "sse"
       }
     }
   }
   ```

## Troubleshooting

**Server fails to start:**
- Ensure both `TEAM_CITY_URL` and `TEAM_CITY_ACCESS_TOKEN` environment variables are set
- Check docker logs: `docker logs teamcity-mcp-server`
- Logs are also written to `logs/TeamCityRemoteMcpServer_*.log` inside the container

**Authentication errors (401):**
- Verify the access token is valid and has not expired
- Ensure the token has permission to read builds in TeamCity

## Contributing

> TBD

## License

See [LICENSE](LICENSE) for details.
