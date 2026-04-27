namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_search_builds"),
        Description(
            "Searches builds across projects using multiple filter criteria. At least one filter must be provided. " +
            "Returns a markdown table with columns: ID, Number, Status, Build Type, Project, Branch, Started, Finished, URL.")]
    public async Task<string> SearchBuilds(
        [Description("Optional project ID to filter by project.")]
        string? projectId = null,

        [Description("Optional build type ID to filter by build configuration.")]
        string? buildTypeId = null,

        [Description("Optional branch name to filter by.")]
        string? branch = null,

        [Description("Optional build status filter: SUCCESS, FAILURE, ERROR, or UNKNOWN.")]
        string? status = null,

        [Description("Optional build state filter: running, finished, or queued.")]
        string? state = null,

        [Description("Optional agent name to filter by.")]
        string? agentName = null,

        [Description("Optional comma-separated tags to filter by.")]
        string? tags = null,

        [Description("Optional start date filter (TeamCity format: yyyyMMddTHHmmss+0000).")]
        string? sinceDate = null,

        [Description("Optional end date filter (TeamCity format: yyyyMMddTHHmmss+0000).")]
        string? untilDate = null,

        [Description("Maximum number of builds to return. Defaults to 25, max 100.")]
        int count = 25)
    {
        var hasFilter = !string.IsNullOrWhiteSpace(projectId)
            || !string.IsNullOrWhiteSpace(buildTypeId)
            || !string.IsNullOrWhiteSpace(branch)
            || !string.IsNullOrWhiteSpace(status)
            || !string.IsNullOrWhiteSpace(state)
            || !string.IsNullOrWhiteSpace(agentName)
            || !string.IsNullOrWhiteSpace(tags)
            || !string.IsNullOrWhiteSpace(sinceDate)
            || !string.IsNullOrWhiteSpace(untilDate);

        if (!hasFilter)
            return "ERROR: At least one search filter is required (projectId, buildTypeId, branch, status, state, agentName, tags, sinceDate, or untilDate).";

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var effectiveCount = Math.Min(Math.Max(count, 1), 100);
            var locatorParts = new List<string> { $"count:{effectiveCount}" };

            if (!string.IsNullOrWhiteSpace(projectId))
                locatorParts.Add($"project:id:{projectId}");
            if (!string.IsNullOrWhiteSpace(buildTypeId))
                locatorParts.Add($"buildType:id:{buildTypeId}");
            if (!string.IsNullOrWhiteSpace(branch))
                locatorParts.Add($"branch:{branch}");
            if (!string.IsNullOrWhiteSpace(status))
                locatorParts.Add($"status:{status.ToUpperInvariant()}");
            if (!string.IsNullOrWhiteSpace(state))
                locatorParts.Add($"state:{state.ToLowerInvariant()}");
            if (!string.IsNullOrWhiteSpace(agentName))
                locatorParts.Add($"agentName:{agentName}");
            if (!string.IsNullOrWhiteSpace(tags))
                locatorParts.Add($"tag:{tags}");
            if (!string.IsNullOrWhiteSpace(sinceDate))
                locatorParts.Add($"sinceDate:{sinceDate}");
            if (!string.IsNullOrWhiteSpace(untilDate))
                locatorParts.Add($"untilDate:{untilDate}");

            var locator = string.Join(",", locatorParts);
            var fields = "build(id,number,status,state,branchName,startDate,finishDate,buildType(name,projectName),webUrl)";
            var url = $"app/rest/builds?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var buildList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildListResponse);

            if (buildList?.Build is null || buildList.Build.Count == 0)
                return "No builds found matching the specified filters.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Search Results");
            sb.AppendLine();
            sb.AppendLine($"**Count:** {buildList.Build.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Number | Status | Build Type | Project | Branch | Started | Finished | URL |");
            sb.AppendLine("|----|--------|--------|------------|---------|--------|---------|----------|-----|");

            foreach (var build in buildList.Build)
            {
                var started = FormatTcDate(build.StartDate);
                var finished = FormatTcDate(build.FinishDate);
                var buildTypeName = build.BuildType?.Name ?? "—";
                var projectName = build.BuildType?.ProjectName ?? "—";
                sb.AppendLine($"| {build.Id} | {build.Number} | {build.Status ?? "—"} | {buildTypeName} | {projectName} | {build.BranchName ?? "default"} | {started} | {finished} | {build.WebUrl} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to search builds — {ex.Message}";
        }
    }
}
