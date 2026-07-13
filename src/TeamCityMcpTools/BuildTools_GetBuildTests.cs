namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_tests"),
        Description(
            "Gets test occurrences for a specific build, including summary counts (passed/failed/ignored/muted) " +
            "and per-test detail such as failure text, whether it's a new failure, and the build it's been " +
            "failing since. Defaults to failed tests only.")]
    public async Task<string> GetBuildTests(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Which tests to include: 'failed' (default), 'muted', or 'all'.")]
        string filter = "failed",

        [Description("Maximum number of test occurrences to return. Defaults to 50.")]
        int count = 50)
    {
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
                $"build:(id:{buildId})",
                $"count:{count}"
            };

            if (string.Equals(filter, "failed", StringComparison.OrdinalIgnoreCase))
                locatorParts.Add("status:FAILURE");
            else if (string.Equals(filter, "muted", StringComparison.OrdinalIgnoreCase))
                locatorParts.Add("muted:true");
            else if (!string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase))
                return $"ERROR: Unknown filter '{filter}'. Use 'failed', 'muted', or 'all'.";

            var locator = string.Join(",", locatorParts);
            var fields = "count,passed,failed,ignored,muted," +
                         "testOccurrence(id,name,status,duration,details,newFailure,muted,firstFailed(build(id,number)))";
            var url = $"app/rest/testOccurrences?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var tests = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.TestOccurrencesResponse);

            if (tests is null)
                return $"ERROR: Unable to parse test occurrences for build '{buildId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Tests");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Filter:** {filter}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Passed | Failed | Ignored | Muted |");
            sb.AppendLine("|--------|--------|---------|-------|");
            sb.AppendLine($"| {tests.Passed ?? 0} | {tests.Failed ?? 0} | {tests.Ignored ?? 0} | {tests.Muted ?? 0} |");
            sb.AppendLine();

            if (tests.TestOccurrence is not { Count: > 0 } occurrences)
            {
                sb.AppendLine($"No test occurrences matched filter '{filter}'.");
                return sb.ToString();
            }

            sb.AppendLine("## Test Occurrences");
            sb.AppendLine();

            foreach (var test in occurrences)
            {
                var icon = test.Status?.ToUpperInvariant() switch
                {
                    "SUCCESS" => "✅",
                    "FAILURE" => "❌",
                    "IGNORED" => "⏭️",
                    _ => "❔"
                };
                var mutedTag = test.Muted == true ? " *(muted)*" : string.Empty;
                var newTag = test.NewFailure == true ? " *(new failure)*" : string.Empty;

                sb.AppendLine($"### {icon} {test.Name}{mutedTag}{newTag}");
                sb.AppendLine();
                sb.AppendLine($"- **Status:** {test.Status}");
                if (test.Duration.HasValue)
                    sb.AppendLine($"- **Duration:** {test.Duration.Value} ms");
                if (test.FirstFailed?.Build is { } firstFailedBuild)
                    sb.AppendLine($"- **Failing since:** build #{firstFailedBuild.Number} (id:{firstFailedBuild.Id})");
                if (!string.IsNullOrWhiteSpace(test.Details))
                    sb.AppendLine($"- **Details:**\n  ```\n  {test.Details.Replace("\n", "\n  ")}\n  ```");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build tests — {ex.Message}";
        }
    }
}
