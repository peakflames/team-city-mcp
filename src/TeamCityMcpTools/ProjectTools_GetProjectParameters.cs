namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetProjectParameters),
        Description(
            "Gets the configuration parameters defined on a TeamCity project — the same values shown in the " +
            "TeamCity project 'Parameters' admin page. Project parameters cascade to every build configuration " +
            "beneath the project, so this is the most consequential place to look when reasoning about config " +
            "shared across many build types. Each parameter shows whether it is defined directly on this project " +
            "or inherited from a parent project, plus its control type and label from the configuration spec. " +
            "Use 'nameFilter' to search by substring — projects can have 100+ params.")]
    public async Task<string> GetProjectParameters(
        [Description("The TeamCity project ID (e.g., 'MyProject').")]
        string projectId,

        [Description("Optional case-insensitive substring to filter parameter names by (e.g. 'branch', 'timeout').")]
        string? nameFilter = null,

        [Description("Which parameters to include: 'all' (default), 'own' (defined directly on this project), " +
                      "or 'inherited' (defined on a parent project).")]
        string inheritance = "all",

        [Description("Maximum number of matching parameters to display. Defaults to 100.")]
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
            var fields = "count,property(name,value,inherited,type(rawValue))";
            var url = $"app/rest/projects/id:{Uri.EscapeDataString(projectId)}/parameters?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Project with ID '{projectId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeParameterListResponse);

            if (parameters is null)
                return $"ERROR: Unable to parse project parameters for '{projectId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Project Parameters");
            sb.AppendLine();
            sb.AppendLine($"**Project ID:** {projectId}");
            sb.AppendLine($"**Total Parameters:** {parameters.Count ?? 0}");
            if (!string.IsNullOrWhiteSpace(nameFilter))
                sb.AppendLine($"**Name Filter:** {nameFilter}");
            if (!string.Equals(inheritance, "all", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"**Inheritance Filter:** {inheritance}");
            sb.AppendLine();

            if (parameters.Property is not { Count: > 0 } allProperties)
            {
                sb.AppendLine("No parameters found for this project.");
                return sb.ToString();
            }

            IEnumerable<BuildTypeParameterEntry> filtered = allProperties;

            if (!string.IsNullOrWhiteSpace(nameFilter))
                filtered = filtered.Where(p => p.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true);

            filtered = inheritance.ToLowerInvariant() switch
            {
                "own" => filtered.Where(p => p.Inherited != true),
                "inherited" => filtered.Where(p => p.Inherited == true),
                _ => filtered,
            };

            var ordered = filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            if (displayed.Count == 0)
            {
                sb.AppendLine("No parameters matched the given filters.");
                return sb.ToString();
            }

            sb.AppendLine($"**Showing:** {displayed.Count} of {ordered.Count} matching parameters");
            sb.AppendLine();
            sb.AppendLine("| Name | Value | Source | Control | Label |");
            sb.AppendLine("|------|-------|--------|---------|-------|");

            foreach (var property in displayed)
            {
                var source = property.Inherited == true ? "inherited (parent project)" : "own";
                var (controlType, label, display) = ParseTypeSpec(property.Type?.RawValue);
                var controlDisplay = string.Equals(display, "hidden", StringComparison.OrdinalIgnoreCase)
                    ? $"{controlType} *(hidden)*"
                    : controlType;
                sb.AppendLine($"| {property.Name} | {property.Value ?? "—"} | {source} | {controlDisplay} | {label ?? "—"} |");
            }

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more parameters not shown — narrow with 'nameFilter' or raise 'count'.*");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get project parameters — {ex.Message}";
        }
    }
}
