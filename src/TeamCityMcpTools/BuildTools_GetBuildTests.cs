namespace TeamCityMcpTools;

public partial class BuildTools
{
    private const int TestDetailsMaxChars = 1200;

    [McpServerTool(Name = "teamcity_get_build_tests"),
        Description(
            "Gets test occurrences for a specific build, including summary counts (passed/failed/ignored/muted) " +
            "and per-test detail such as failure text, whether it's a new failure, and the build it's been " +
            "failing since. Defaults to failed tests only. Tests whose failure text is identical (e.g. many " +
            "tests failing on the same root cause) are grouped together to keep output compact.")]
    public async Task<string> GetBuildTests(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Which tests to include: 'failed' (default), 'muted', or 'all'.")]
        string filter = "failed",

        [Description("Maximum number of test occurrences to return. Defaults to 50.")]
        int count = 50,

        [Description("How much failure text to show per failure group: 'compact' (default, one-line preview), " +
                      "'full' (truncated failure text per group), or 'none' (table only, no failure text).")]
        string detailsMode = "compact",

        [Description("Maximum characters of failure text to show per group when detailsMode is 'full'. Defaults to 1200.")]
        int detailsMaxChars = TestDetailsMaxChars)
    {
        if (!string.Equals(detailsMode, "compact", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(detailsMode, "full", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(detailsMode, "none", StringComparison.OrdinalIgnoreCase))
            return $"ERROR: Unknown detailsMode '{detailsMode}'. Use 'compact', 'full', or 'none'.";
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

            // Group tests with identical failure text so a shared root cause (common when many tests fail
            // the same way) is shown once instead of once per test.
            var groupKeyToLetter = new Dictionary<string, string>();
            var groupLetterToDetails = new Dictionary<string, string>();
            var groupLetterToCount = new Dictionary<string, int>();
            var testGroupLetters = new string?[occurrences.Count];

            for (var i = 0; i < occurrences.Count; i++)
            {
                var details = occurrences[i].Details;
                if (string.IsNullOrWhiteSpace(details))
                    continue;

                var key = TeamCityFormat.Fingerprint(details);
                if (!groupKeyToLetter.TryGetValue(key, out var letter))
                {
                    letter = IndexToLetters(groupKeyToLetter.Count);
                    groupKeyToLetter[key] = letter;
                    groupLetterToDetails[letter] = details;
                }

                testGroupLetters[i] = letter;
                groupLetterToCount[letter] = groupLetterToCount.GetValueOrDefault(letter) + 1;
            }

            var hasGroups = groupKeyToLetter.Count > 0;

            sb.AppendLine("## Test Occurrences");
            sb.AppendLine();
            sb.Append("| # | Status | Test | Failing since |");
            if (hasGroups)
                sb.Append(" Group |");
            sb.AppendLine();
            sb.Append("|---|--------|------|----------------|");
            if (hasGroups)
                sb.Append("-------|");
            sb.AppendLine();

            for (var i = 0; i < occurrences.Count; i++)
            {
                var test = occurrences[i];
                var icon = test.Status?.ToUpperInvariant() switch
                {
                    "SUCCESS" => "✅",
                    "FAILURE" => "❌",
                    "IGNORED" => "⏭️",
                    _ => "❔"
                };
                var mutedTag = test.Muted == true ? " *(muted)*" : string.Empty;
                var newTag = test.NewFailure == true ? " *(new)*" : string.Empty;
                var failingSince = test.FirstFailed?.Build is { } firstFailedBuild
                    ? $"#{firstFailedBuild.Number}"
                    : "—";
                var duration = test.Duration.HasValue ? $" ({test.Duration.Value} ms)" : string.Empty;

                sb.Append($"| {i + 1} | {icon} {test.Status}{duration} | {test.Name}{mutedTag}{newTag} | {failingSince} |");
                if (hasGroups)
                    sb.Append($" {testGroupLetters[i] ?? "—"} |");
                sb.AppendLine();
            }
            sb.AppendLine();

            if (hasGroups && !string.Equals(detailsMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("## Failure Groups");
                sb.AppendLine();
                sb.AppendLine("Tests with identical failure text are grouped once below instead of repeating it per test.");
                sb.AppendLine();

                foreach (var (letter, details) in groupLetterToDetails.OrderBy(kv => kv.Key))
                {
                    var groupCount = groupLetterToCount[letter];
                    sb.AppendLine($"### Group {letter} — {groupCount} test(s)");
                    sb.AppendLine();

                    if (string.Equals(detailsMode, "full", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine($"```\n{TeamCityFormat.Truncate(details, detailsMaxChars)}\n```");
                    else
                        sb.AppendLine($"```\n{TeamCityFormat.Preview(details)}\n```");

                    sb.AppendLine();
                }
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build tests — {ex.Message}";
        }
    }

    private static string IndexToLetters(int index)
    {
        // 0 -> A, 1 -> B, ..., 25 -> Z, 26 -> AA, ...
        var chars = new List<char>();
        do
        {
            chars.Insert(0, (char)('A' + index % 26));
            index = index / 26 - 1;
        } while (index >= 0);

        return new string(chars.ToArray());
    }
}
