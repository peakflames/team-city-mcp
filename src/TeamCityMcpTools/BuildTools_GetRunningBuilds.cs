namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_running_builds"),
        Description(
            "Gets all currently running builds, optionally filtered by project. " +
            "Returns a markdown table with columns: ID, Number, Build Type, Project, Branch, Agent, Started, Progress%, URL.")]
    public async Task<string> GetRunningBuilds(
        [Description("Optional project ID to filter running builds by project.")]
        string? projectId = null)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var locatorParts = new List<string> { "state:running" };
            if (!string.IsNullOrWhiteSpace(projectId))
                locatorParts.Add($"project:id:{projectId}");

            var locator = string.Join(",", locatorParts);
            var fields = "build(id,number,status,branchName,startDate,percentageComplete,agent(name),buildType(name,projectName),webUrl)";
            var url = $"app/rest/builds?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var buildList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildListResponse);

            if (buildList?.Build is null || buildList.Build.Count == 0)
                return "No running builds at this time.";

            var sb = new StringBuilder();
            sb.AppendLine("# Running Builds");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            sb.AppendLine($"**Count:** {buildList.Build.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Number | Build Type | Project | Branch | Agent | Started | Progress% | URL |");
            sb.AppendLine("|----|--------|------------|---------|--------|-------|---------|-----------|-----|");

            foreach (var build in buildList.Build)
            {
                var started = FormatTcDate(build.StartDate);
                var progress = build.PercentageComplete.HasValue ? $"{build.PercentageComplete}%" : "—";
                var agent = build.Agent?.Name ?? "—";
                var buildTypeName = build.BuildType?.Name ?? "—";
                var projectName = build.BuildType?.ProjectName ?? "—";
                sb.AppendLine($"| {build.Id} | {build.Number} | {buildTypeName} | {projectName} | {build.BranchName ?? "default"} | {agent} | {started} | {progress} | {build.WebUrl} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get running builds — {ex.Message}";
        }
    }
}
