namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_parameters"),
        Description(
            "Gets the resulting build parameters (configuration parameters, system properties, and " +
            "environment variables) that were actually applied to a build after all overrides. " +
            "Real builds can have 1000+ properties — use 'nameFilter' to search by substring, " +
            "otherwise the list is capped at 'count' entries.")]
    public async Task<string> GetBuildParameters(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Optional case-insensitive substring to filter parameter names by (e.g. 'version', 'agent').")]
        string? nameFilter = null,

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
            var url = $"app/rest/builds/id:{buildId}/resulting-properties";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ParameterListResponse);

            if (parameters is null)
                return $"ERROR: Unable to parse build parameters for build '{buildId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Parameters");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Total Properties:** {parameters.Count ?? 0}");
            if (!string.IsNullOrWhiteSpace(nameFilter))
                sb.AppendLine($"**Name Filter:** {nameFilter}");
            sb.AppendLine();

            if (parameters.Property is not { Count: > 0 } allProperties)
            {
                sb.AppendLine("No resulting properties found for this build.");
                return sb.ToString();
            }

            var filtered = string.IsNullOrWhiteSpace(nameFilter)
                ? allProperties
                : allProperties.Where(p => p.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();

            var ordered = filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            if (displayed.Count == 0)
            {
                sb.AppendLine($"No properties matched filter '{nameFilter}'.");
                return sb.ToString();
            }

            sb.AppendLine($"**Showing:** {displayed.Count} of {ordered.Count} matching properties");
            sb.AppendLine();
            sb.AppendLine("| Name | Value |");
            sb.AppendLine("|------|-------|");

            foreach (var property in displayed)
                sb.AppendLine($"| {property.Name} | {property.Value} |");

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more properties not shown — narrow with 'nameFilter' or raise 'count'.*");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build parameters — {ex.Message}";
        }
    }
}
