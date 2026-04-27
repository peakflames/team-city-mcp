namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_status"),
        Description(
            "Gets a compact status summary for a specific build. " +
            "Returns a single-line status showing state, branch, progress (if running), and URL.")]
    public async Task<string> GetBuildStatus(
        [Description("The TeamCity build ID (numeric).")]
        string buildId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "id,number,status,state,statusText,percentageComplete,branchName,webUrl";
            var url = $"app/rest/builds/id:{buildId}?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var build = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildStatusSummary);

            if (build is null)
                return $"ERROR: Unable to parse build status for ID '{buildId}'.";

            var branch = build.BranchName ?? "default";

            if (string.Equals(build.State, "running", StringComparison.OrdinalIgnoreCase))
            {
                var progress = build.PercentageComplete.HasValue ? $"{build.PercentageComplete}%" : "unknown%";
                return $"Build #{build.Number} — RUNNING ({progress}) on branch '{branch}' — {build.WebUrl}";
            }
            else
            {
                var status = build.Status ?? build.State ?? "UNKNOWN";
                var detail = string.IsNullOrWhiteSpace(build.StatusText) ? string.Empty : $" ({build.StatusText})";
                return $"Build #{build.Number} — {status}{detail} on branch '{branch}' — {build.WebUrl}";
            }
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build status — {ex.Message}";
        }
    }
}
