namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetProjectFeatures),
        Description(
            "Gets the project features configured on a TeamCity project — report tabs, build/project graphs, " +
            "versioned settings, issue trackers, and similar project-level integrations shown in the TeamCity " +
            "project admin pages. Features are grouped by type, and each is marked as 'own' (defined directly on " +
            "this project) or 'inherited' (defined on a parent project), plus whether it is disabled. Does NOT " +
            "include configuration parameters — use 'teamcity_get_project_parameters' for those.")]
    public async Task<string> GetProjectFeatures(
        [Description("The TeamCity project ID (e.g., 'MyProject').")]
        string projectId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "count,projectFeature(id,type,disabled,inherited,properties(property(name,value)))";
            var url = $"app/rest/projects/id:{projectId}/projectFeatures?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Project with ID '{projectId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var features = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ProjectFeaturesWrapper);

            if (features is null)
                return $"ERROR: Unable to parse project features for '{projectId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Project Features");
            sb.AppendLine();
            sb.AppendLine($"**Project ID:** {projectId}");
            sb.AppendLine($"**Total Features:** {features.Count ?? 0}");
            sb.AppendLine();

            var allFeatures = features.ProjectFeature;
            if (allFeatures is not { Count: > 0 })
            {
                sb.AppendLine("No project features configured.");
                return sb.ToString();
            }

            var groups = allFeatures
                .GroupBy(f => f.Type ?? "Unknown")
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();

                foreach (var feature in group)
                {
                    var origin = feature.Inherited == true ? " *(inherited)*" : " *(own)*";
                    var disabled = feature.Disabled == true ? " *(disabled)*" : "";
                    sb.AppendLine($"### {feature.Id ?? "Feature"}{origin}{disabled}");
                    sb.AppendLine();

                    var props = feature.Properties?.Property;
                    if (props is { Count: > 0 })
                    {
                        sb.AppendLine("| Property | Value |");
                        sb.AppendLine("|----------|-------|");
                        foreach (var prop in props.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            var value = (prop.Value ?? "—").Replace("\r\n", " ").Replace('\n', ' ').Replace("|", "\\|");
                            sb.AppendLine($"| {prop.Name} | {value} |");
                        }
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine("No properties.");
                        sb.AppendLine();
                    }
                }
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get project features — {ex.Message}";
        }
    }
}
