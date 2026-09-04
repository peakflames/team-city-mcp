namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = TeamCityToolNames.GetTestHistory),
        Description(
            "Follows a single test by name across builds, optionally scoped to a build type, showing " +
            "status/duration/branch per run. Useful for spotting flakiness or when a test started failing.")]
    public async Task<string> GetTestHistory(
        [Description("The fully-qualified test name to look up.")]
        string testName,

        [Description("Optional build type ID to scope the history to. Defaults to server-wide.")]
        string? buildTypeId = null,

        [Description("Maximum number of test occurrences to return. Defaults to 25.")]
        int count = 25)
    {
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
            var locatorParts = new List<string>
            {
                $"test:(name:({TeamCityLocator.EscapeValue(testName)}))",
                $"count:{count}"
            };
            if (!string.IsNullOrWhiteSpace(buildTypeId))
                locatorParts.Add($"buildType:(id:{buildTypeId})");

            var locator = string.Join(",", locatorParts);
            var fields = "count,testOccurrence(status,duration,build(id,number,branchName,buildType(id,name,projectId)))";
            var url = $"app/rest/testOccurrences?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var tests = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.TestOccurrencesResponse);

            var occurrences = (IReadOnlyList<TestOccurrence>?)tests?.TestOccurrence ?? [];
            occurrences = await ToolGate.FilterByVisibleSetAsync(
                _serviceProvider, TeamCityToolNames.GetTestHistory, occurrences, t => t.Build?.BuildType?.ProjectId);

            if (occurrences.Count == 0)
                return $"No test history found for '{testName}'" + (string.IsNullOrWhiteSpace(buildTypeId) ? "." : $" in build type '{buildTypeId}'.");

            var sb = new StringBuilder();
            sb.AppendLine("# Test History");
            sb.AppendLine();
            sb.AppendLine($"**Test:** {testName}");
            if (!string.IsNullOrWhiteSpace(buildTypeId))
                sb.AppendLine($"**Build Type:** {buildTypeId}");
            sb.AppendLine($"**Count:** {occurrences.Count}");
            sb.AppendLine();
            sb.AppendLine("| Build # | Status | Duration | Branch | Build Type |");
            sb.AppendLine("|---------|--------|----------|--------|------------|");

            foreach (var test in occurrences)
            {
                var build = test.Build;
                var duration = test.Duration.HasValue ? TeamCityFormat.FormatDurationMs(test.Duration.Value) : "—";
                sb.AppendLine($"| {build?.Number} | {test.Status} | {duration} | {build?.BranchName ?? "default"} | {build?.BuildType?.Name} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get test history — {ex.Message}";
        }
    }
}
