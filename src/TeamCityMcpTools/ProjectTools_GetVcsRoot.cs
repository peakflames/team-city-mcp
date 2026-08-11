namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetVcsRoot),
        Description(
            "Gets full connection details for a specific TeamCity VCS root — repository URL, branch spec, " +
            "authentication method, and other VCS-type-specific settings. Secure properties (e.g. passwords) " +
            "are masked and never shown. Returns a markdown document.")]
    public async Task<string> GetVcsRoot(
        [Description("The TeamCity VCS root ID (e.g., 'FlightSwBmsRootProject').")]
        string vcsRootId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "id,name,vcsName,project(id,name),properties(property(name,value))";
            var url = $"app/rest/vcs-roots/id:{vcsRootId}?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: VCS root with ID '{vcsRootId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.VcsRootDetails);

            if (root is null)
                return $"ERROR: Unable to parse VCS root details for ID '{vcsRootId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# VCS Root: {root.Name}");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| ID | {root.Id} |");
            sb.AppendLine($"| Name | {root.Name} |");
            sb.AppendLine($"| Type | {root.VcsName ?? "—"} |");
            sb.AppendLine($"| Project | {root.Project?.Name ?? "—"} (`{root.Project?.Id ?? "—"}`) |");
            sb.AppendLine();

            var props = root.Properties?.Property;
            sb.AppendLine("## Connection Settings");
            sb.AppendLine();
            if (props is { Count: > 0 })
            {
                sb.AppendLine("| Property | Value |");
                sb.AppendLine("|----------|-------|");
                foreach (var prop in props.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var isSecure = prop.Name?.StartsWith("secure:", StringComparison.OrdinalIgnoreCase) == true;
                    var value = isSecure ? "•••• (secure, hidden)" : (string.IsNullOrEmpty(prop.Value) ? "—" : prop.Value);
                    sb.AppendLine($"| {prop.Name} | {value} |");
                }
            }
            else
            {
                sb.AppendLine("No connection properties found.");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get VCS root — {ex.Message}";
        }
    }
}
