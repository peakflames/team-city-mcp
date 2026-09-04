namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetBuildTypeFeatures),
        Description(
            "Gets the build features configured on a TeamCity build configuration — this is where the Matrix Build " +
            "feature (type `matrix`) lives, along with things like build failure conditions, swabra, notifications, " +
            "and commit status publishers. Use this when diagnosing a matrix/composite build configuration, or any " +
            "other build-type-level feature. Features are grouped by type, and each is marked as 'own' (defined " +
            "directly on this build type) or 'inherited' (defined on an attached template), plus whether it is " +
            "disabled. Does NOT include configuration parameters — use 'teamcity_get_build_type_parameters' for those.")]
    public async Task<string> GetBuildTypeFeatures(
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
            var fields = "count,feature(id,type,disabled,inherited,properties(property(name,value)))";
            var url = $"app/rest/buildTypes/id:{Uri.EscapeDataString(buildTypeId)}/features?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build type with ID '{buildTypeId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var features = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeFeaturesWrapper);

            if (features is null)
                return $"ERROR: Unable to parse build type features for '{buildTypeId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Type Features");
            sb.AppendLine();
            sb.AppendLine($"**Build Type ID:** {buildTypeId}");
            sb.AppendLine($"**Total Features:** {features.Count ?? 0}");
            sb.AppendLine();

            var allFeatures = features.Feature;
            if (allFeatures is not { Count: > 0 })
            {
                sb.AppendLine("No build features configured.");
                return sb.ToString();
            }

            var groups = allFeatures
                .GroupBy(f => f.Type ?? "Unknown")
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var matrixNote = string.Equals(group.Key, "matrix", StringComparison.OrdinalIgnoreCase)
                    ? " *(Matrix Build feature — fans this build out into multiple sub-builds)*"
                    : string.Empty;
                sb.AppendLine($"## {group.Key}{matrixNote}");
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
            return $"ERROR: Failed to get build type features — {ex.Message}";
        }
    }
}
