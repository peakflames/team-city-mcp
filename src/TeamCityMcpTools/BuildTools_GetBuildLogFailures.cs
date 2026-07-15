namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_log_failures"),
        Description(
            "For a build's failed tests, finds and returns the console log context around each failure by " +
            "searching the build log for each failed test's name. Combines the test's failure details from the " +
            "TeamCity API with matching log output (assertions, stack traces, teardown errors). Use this instead " +
            "of teamcity_search_build_log when you already know a build failed and want failure context for " +
            "every failed test in one call.")]
    public async Task<string> GetBuildLogFailures(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Number of lines of context to include before and after each log match. Defaults to 6.")]
        int contextLines = 6,

        [Description("Maximum number of log match windows to return per failed test. Defaults to 3.")]
        int maxMatchesPerTest = 3)
    {
        if (contextLines < 0)
            return "ERROR: contextLines must be >= 0.";
        if (maxMatchesPerTest < 1)
            return "ERROR: maxMatchesPerTest must be >= 1.";

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var locator = $"build:(id:{buildId}),status:FAILURE";
            var fields = "count,testOccurrence(id,name,status,duration,details)";
            var testsUrl = $"app/rest/testOccurrences?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var testsResponse = await client.HttpClient.GetAsync(testsUrl);

            if (testsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (testsResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!testsResponse.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)testsResponse.StatusCode}: {testsResponse.ReasonPhrase}";

            var testsJson = await testsResponse.Content.ReadAsStringAsync();
            var tests = JsonSerializer.Deserialize(testsJson, TeamCityJsonContext.Default.TestOccurrencesResponse);

            var sb = new StringBuilder();
            sb.AppendLine("# Build Log Failures");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine();

            if (tests?.TestOccurrence is not { Count: > 0 } failedTests)
            {
                sb.AppendLine(
                    "No failed tests found for this build. Use teamcity_search_build_log with a custom pattern " +
                    "(e.g. \"FAILED\", \"ERROR\", or \"Exception\") to inspect the log directly.");
                return sb.ToString();
            }

            var logResponse = await GetBuildLogResponseAsync(client, buildId);

            if (logResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (logResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build log for build '{buildId}' was not found.";
            if (!logResponse.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)logResponse.StatusCode}: {logResponse.ReasonPhrase}";

            var (lines, scanCapHit) = await ReadLogLinesAsync(logResponse, LogSearchMaxScanBytes);

            sb.AppendLine($"**Failed tests:** {failedTests.Count}");
            sb.AppendLine($"**Log lines scanned:** {lines.Count:N0}{(scanCapHit ? $" (stopped at {LogSearchMaxScanBytes:N0} bytes)" : string.Empty)}");
            sb.AppendLine();

            var budget = LogSearchMaxOutputBytes;
            var skippedForBudget = 0;

            foreach (var test in failedTests)
            {
                sb.AppendLine($"## ❌ {test.Name}");
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(test.Details))
                    sb.AppendLine($"**Details:**\n```\n{test.Details}\n```\n");

                if (budget <= 0)
                {
                    skippedForBudget++;
                    sb.AppendLine("*(log search skipped — output budget reached)*");
                    sb.AppendLine();
                    continue;
                }

                var key = ExtractLeafSegment(test.Name);
                if (key.Length == 0)
                {
                    sb.AppendLine("*(could not derive a search key from this test's name)*");
                    sb.AppendLine();
                    continue;
                }

                var regex = new Regex(Regex.Escape(key), MakeRegexOptions(ignoreCase: true));
                var matcher = new LogWindowMatcher(regex, contextLines, maxMatchesPerTest);
                for (var i = 0; i < lines.Count; i++)
                {
                    if (matcher.ProcessLine(i + 1, lines[i]))
                        break;
                }
                matcher.Finish();

                if (matcher.Windows.Count == 0)
                {
                    sb.AppendLine($"*(no log lines matched \"{key}\")*");
                    sb.AppendLine();
                    continue;
                }

                budget = AppendLogWindows(sb, matcher.Windows, budget);
                sb.AppendLine();
            }

            if (skippedForBudget > 0)
                sb.AppendLine($"**Note:** Skipped log search for {skippedForBudget} test(s) after reaching the ~{LogSearchMaxOutputBytes:N0} byte output budget.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build log failures — {ex.Message}";
        }
    }

    private static string ExtractLeafSegment(string? testName)
    {
        if (string.IsNullOrWhiteSpace(testName))
            return string.Empty;

        var lastDot = testName.LastIndexOf('.');
        return lastDot >= 0 && lastDot < testName.Length - 1 ? testName[(lastDot + 1)..] : testName;
    }
}
