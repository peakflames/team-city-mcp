namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = TeamCityToolNames.SearchBuilds),
        Description(
            "Searches builds across projects using multiple filter criteria. At least one filter must be provided. " +
            "Returns a markdown table with columns: ID, Number, Status, Build Type, Project, Branch, Started, Finished, URL.")]
    public async Task<string> SearchBuilds(
        [Description("Optional project ID to filter by project, including its subprojects.")]
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

        if (!string.IsNullOrWhiteSpace(projectId) && !TeamCityLocator.IsSafeId(projectId))
            return $"ERROR: Invalid projectId '{projectId}'.";
        if (!string.IsNullOrWhiteSpace(buildTypeId) && !TeamCityLocator.IsSafeId(buildTypeId))
            return $"ERROR: Invalid buildTypeId '{buildTypeId}'.";

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
                locatorParts.Add($"affectedProject:(id:{projectId})");
            if (!string.IsNullOrWhiteSpace(buildTypeId))
                locatorParts.Add($"buildType:(id:{buildTypeId})");
            if (!string.IsNullOrWhiteSpace(branch))
                locatorParts.Add(TeamCityLocator.Dimension("branch", branch));
            if (!string.IsNullOrWhiteSpace(status))
                locatorParts.Add(TeamCityLocator.Dimension("status", status.ToUpperInvariant()));
            if (!string.IsNullOrWhiteSpace(state))
                locatorParts.Add(TeamCityLocator.Dimension("state", state.ToLowerInvariant()));
            if (!string.IsNullOrWhiteSpace(agentName))
                locatorParts.Add(TeamCityLocator.Dimension("agentName", agentName));
            if (!string.IsNullOrWhiteSpace(tags))
                locatorParts.Add(TeamCityLocator.Dimension("tag", tags));
            if (!string.IsNullOrWhiteSpace(sinceDate))
                locatorParts.Add(TeamCityLocator.Dimension("sinceDate", sinceDate));
            if (!string.IsNullOrWhiteSpace(untilDate))
                locatorParts.Add(TeamCityLocator.Dimension("untilDate", untilDate));

            var locator = string.Join(",", locatorParts);
            var fields = "build(id,number,status,state,branchName,startDate,finishDate,buildType(id,name,projectId,projectName),webUrl)";
            var url = $"app/rest/builds?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var buildList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildListResponse);

            var builds = (IReadOnlyList<BuildSummary>?)buildList?.Build ?? [];
            builds = await ToolGate.FilterByVisibleSetAsync(
                _serviceProvider, TeamCityToolNames.SearchBuilds, builds, b => b.BuildType?.ProjectId);

            if (builds.Count == 0)
                return "No builds found matching the specified filters.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Search Results");
            sb.AppendLine();
            sb.AppendLine($"**Count:** {builds.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Number | Status | Build Type | Project | Branch | Started | Finished | URL |");
            sb.AppendLine("|----|--------|--------|------------|---------|--------|---------|----------|-----|");

            foreach (var build in builds)
            {
                var started = TeamCityFormat.FormatTcDate(build.StartDate);
                var finished = TeamCityFormat.FormatTcDate(build.FinishDate);
                var buildTypeName = build.BuildType?.Name ?? "—";
                var projectName = build.BuildType?.ProjectName ?? "—";
                sb.AppendLine($"| {build.Id} | {build.Number} | {build.Status ?? "—"} | {buildTypeName} | {projectName} | {build.BranchName ?? "default"} | {started} | {finished} | {build.WebUrl} |");
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to search builds — {ex.Message}";
        }
    }
}
