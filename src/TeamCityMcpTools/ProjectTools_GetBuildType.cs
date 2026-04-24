namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_build_type"),
        Description(
            "Gets full details for a specific TeamCity build configuration, including VCS root info and a direct web URL. " +
            "Returns a markdown document with build type metadata and VCS roots.")]
    public async Task<string> GetBuildType(
        [Description("The TeamCity build type ID (e.g., 'MyProject_Build').")]
        string buildTypeId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "id,name,description,projectId,projectName,paused,webUrl,vcsRoots(vcsRootEntry(id,vcsRoot(id,name,vcsName)))";
            var url = $"app/rest/buildTypes/id:{buildTypeId}?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build type with ID '{buildTypeId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var bt = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeDetails);

            if (bt is null)
                return $"ERROR: Unable to parse build type details for ID '{buildTypeId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Build Configuration: {bt.Name}");
            sb.AppendLine();
            sb.AppendLine("## Details");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| ID | {bt.Id} |");
            sb.AppendLine($"| Name | {bt.Name} |");
            sb.AppendLine($"| Description | {(string.IsNullOrWhiteSpace(bt.Description) ? "—" : bt.Description)} |");
            sb.AppendLine($"| Project ID | {bt.ProjectId} |");
            sb.AppendLine($"| Project Name | {bt.ProjectName} |");
            sb.AppendLine($"| Paused | {(bt.Paused == true ? "Yes" : "No")} |");
            sb.AppendLine($"| URL | {bt.WebUrl ?? "—"} |");
            sb.AppendLine();

            var entries = bt.VcsRoots?.VcsRootEntry;
            if (entries is { Count: > 0 })
            {
                sb.AppendLine("## VCS Roots");
                sb.AppendLine();
                sb.AppendLine("| Entry ID | VCS Root ID | Name | Type |");
                sb.AppendLine("|----------|-------------|------|------|");
                foreach (var entry in entries)
                {
                    var vcs = entry.VcsRoot;
                    sb.AppendLine($"| {entry.Id} | {vcs?.Id ?? "—"} | {vcs?.Name ?? "—"} | {vcs?.VcsName ?? "—"} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("## VCS Roots");
                sb.AppendLine();
                sb.AppendLine("No VCS roots configured.");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build type — {ex.Message}";
        }
    }
}
