namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.ListVcsRoots),
        Description(
            "Lists TeamCity VCS roots (id and name only). Optionally scoped to a specific project. " +
            "Use 'teamcity_get_vcs_root' for full connection details on a single root. " +
            "Returns a markdown table.")]
    public async Task<string> ListVcsRoots(
        [Description("Optional project ID to scope the results. When omitted, all VCS roots across all projects are returned.")]
        string? projectId = null,

        [Description("Optional case-insensitive substring to filter VCS root names by.")]
        string? nameFilter = null,

        [Description("Maximum number of matching VCS roots to display. Defaults to 100.")]
        int count = 100)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "count,vcs-root(id,name)";
            var url = string.IsNullOrWhiteSpace(projectId)
                ? $"app/rest/vcs-roots?fields={Uri.EscapeDataString(fields)}"
                : $"app/rest/vcs-roots?locator={Uri.EscapeDataString($"project:(id:{projectId})")}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return string.IsNullOrWhiteSpace(projectId)
                    ? "ERROR: VCS roots endpoint was not found."
                    : $"ERROR: Project with ID '{projectId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var rootList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.VcsRootListResponse);

            if (rootList?.VcsRoot is not { Count: > 0 } allRoots)
            {
                return string.IsNullOrWhiteSpace(projectId)
                    ? "No VCS roots found."
                    : $"No VCS roots found for project '{projectId}'.";
            }

            var filtered = string.IsNullOrWhiteSpace(nameFilter)
                ? allRoots
                : allRoots.Where(r => r.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();

            var ordered = filtered.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# VCS Roots");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            if (!string.IsNullOrWhiteSpace(nameFilter))
                sb.AppendLine($"**Name Filter:** {nameFilter}");
            sb.AppendLine($"**Total:** {ordered.Count}");
            sb.AppendLine();

            if (displayed.Count == 0)
            {
                sb.AppendLine($"No VCS roots matched filter '{nameFilter}'.");
                return sb.ToString();
            }

            sb.AppendLine("| ID | Name |");
            sb.AppendLine("|----|------|");
            foreach (var root in displayed)
                sb.AppendLine($"| {root.Id} | {root.Name} |");

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more VCS roots not shown — narrow with 'nameFilter' or raise 'count'.*");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list VCS roots — {ex.Message}";
        }
    }
}
