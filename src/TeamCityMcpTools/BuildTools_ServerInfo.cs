namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_server_info"),
        Description(
            "Gets TeamCity server version and instance metadata. " +
            "Returns a markdown document with version, build number, start time, current time, and server URL.")]
    public async Task<string> GetServerInfo()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var response = await client.HttpClient.GetAsync("app/rest/server");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var server = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ServerInfo);

            if (server is null)
                return "ERROR: Unable to parse server info response.";

            var sb = new StringBuilder();
            sb.AppendLine("# TeamCity Server Info");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| Version | {server.Version ?? "—"} |");
            sb.AppendLine($"| Build Number | {server.BuildNumber ?? "—"} |");
            sb.AppendLine($"| Started | {FormatTcDate(server.StartTime)} |");
            sb.AppendLine($"| Current Time | {FormatTcDate(server.CurrentTime)} |");
            sb.AppendLine($"| URL | {server.WebUrl ?? "—"} |");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get server info — {ex.Message}";
        }
    }
}
