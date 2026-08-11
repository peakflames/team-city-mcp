namespace TeamCityMcpTools;

public partial class BuildTools
{
    private const int TestDetailsMaxChars = 1200;
    private const int MaxChainPartsRendered = 30;

    [McpServerTool(Name = TeamCityToolNames.GetBuildTests),
        Description(
            "Gets test occurrences for a specific build, including summary counts (passed/failed/ignored/muted/newFailed) " +
            "and per-test detail such as failure text, whether it's a new failure, and the build it's been failing since. " +
            "Defaults to failed tests only — the 'failed' filter excludes muted tests (status:FAILURE,muted:false), matching " +
            "what the TeamCity UI reports; use filter 'muted' to see muted failures separately. Summary counts always reflect " +
            "the whole build (not just the page returned by 'count'). Composite/matrix builds are handled natively: TeamCity " +
            "aggregates test occurrences across the entire build chain automatically, so results below already include every " +
            "sub-build — no need to query each one separately. For a composite build, occurrences carry a Sub-build column " +
            "and a 'Chain parts' section lists each direct sub-build with its own accurate totals for drill-down. Tests whose " +
            "failure text is identical (e.g. many tests failing on the same root cause) are grouped together to keep output " +
            "compact.")]
    public async Task<string> GetBuildTests(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Which tests to include: 'failed' (default, excludes muted), 'muted', or 'all'.")]
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

        if (!string.Equals(filter, "failed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(filter, "muted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase))
            return $"ERROR: Unknown filter '{filter}'. Use 'failed', 'muted', or 'all'.";

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var summaryFields = "id,number,composite,buildType(id,name)," +
                                 "testOccurrences(count,passed,failed,ignored,muted,newFailed)," +
                                 "snapshot-dependencies(build(id,number,status,composite,buildType(id,name)))";
            var summaryUrl = $"app/rest/builds/id:{buildId}?fields={Uri.EscapeDataString(summaryFields)}";
            var summaryResponse = await client.HttpClient.GetAsync(summaryUrl);

            if (summaryResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (summaryResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!summaryResponse.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)summaryResponse.StatusCode}: {summaryResponse.ReasonPhrase}";

            var summaryJson = await summaryResponse.Content.ReadAsStringAsync();
            var summary = JsonSerializer.Deserialize(summaryJson, TeamCityJsonContext.Default.BuildCompositeSummary);

            if (summary is null)
                return $"ERROR: Unable to parse build details for '{buildId}'.";

            var isComposite = summary.Composite == true;

            var locatorParts = new List<string>
            {
                $"build:(id:{buildId})",
                $"count:{count}"
            };

            if (string.Equals(filter, "failed", StringComparison.OrdinalIgnoreCase))
                locatorParts.Add("status:FAILURE,muted:false");
            else if (string.Equals(filter, "muted", StringComparison.OrdinalIgnoreCase))
                locatorParts.Add("muted:true");

            var locator = string.Join(",", locatorParts);
            var occurrenceFields = "testOccurrence(id,name,status,duration,details,newFailure,muted,firstFailed(build(id,number))" +
                                    (isComposite ? ",build(id,number,buildType(name))" : string.Empty) + ")";
            var url = $"app/rest/testOccurrences?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(occurrenceFields)}";

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
            if (isComposite)
            {
                sb.AppendLine();
                sb.AppendLine("*This is a composite/matrix build — TeamCity aggregates test occurrences natively across the " +
                              "entire build chain, so the results below already cover every sub-build. See \"Chain parts\" " +
                              "below for accurate per-sub-build totals.*");
            }
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();

            var totals = summary.TestOccurrences;
            sb.AppendLine("| Passed | Failed | Ignored | Muted | New Failed |");
            sb.AppendLine("|--------|--------|---------|-------|------------|");
            sb.AppendLine($"| {totals?.Passed ?? 0} | {totals?.Failed ?? 0} | {totals?.Ignored ?? 0} | {totals?.Muted ?? 0} | {totals?.NewFailed ?? 0} |");
            sb.AppendLine();

            if (tests.TestOccurrence is not { Count: > 0 } occurrences)
            {
                sb.AppendLine($"No test occurrences matched filter '{filter}'.");
                if (isComposite)
                    await AppendChainParts(client, sb, summary);
                return TeamCityFormat.Clamp(sb.ToString());
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
            if (isComposite)
                sb.Append(" Sub-build |");
            if (hasGroups)
                sb.Append(" Group |");
            sb.AppendLine();
            sb.Append("|---|--------|------|----------------|");
            if (isComposite)
                sb.Append("-----------|");
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
                var duration = test.Duration.HasValue ? $" ({TeamCityFormat.FormatDurationMs(test.Duration.Value)})" : string.Empty;

                sb.Append($"| {i + 1} | {icon} {test.Status}{duration} | {test.Name}{mutedTag}{newTag} | {failingSince} |");
                if (isComposite)
                {
                    var subBuild = test.Build is { } b ? $"{b.BuildType?.Name ?? "—"} #{b.Number} (id:{b.Id})" : "—";
                    sb.Append($" {subBuild} |");
                }
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

            if (isComposite)
                await AppendChainParts(client, sb, summary);

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build tests — {ex.Message}";
        }
    }

    private static async Task AppendChainParts(TeamCityClient client, StringBuilder sb, BuildCompositeSummary summary)
    {
        var parts = summary.SnapshotDependencies?.Build;
        sb.AppendLine("## Chain Parts");
        sb.AppendLine();

        if (parts is not { Count: > 0 })
        {
            sb.AppendLine("No direct snapshot-dependency sub-builds found.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("Direct sub-builds of this composite build, each with its own accurate totals. Re-run this tool with " +
                      "a sub-build's ID for its full test detail.");
        sb.AppendLine();
        sb.AppendLine("| Build ID | Build Type | Number | Status | Passed | Failed | Ignored | Muted | New Failed |");
        sb.AppendLine("|----------|------------|--------|--------|--------|--------|---------|-------|------------|");

        foreach (var part in parts.Take(MaxChainPartsRendered))
        {
            var totalsFields = "testOccurrences(count,passed,failed,ignored,muted,newFailed)";
            var totalsUrl = $"app/rest/builds/id:{part.Id}?fields={Uri.EscapeDataString(totalsFields)}";
            var totalsResponse = await client.HttpClient.GetAsync(totalsUrl);

            if (!totalsResponse.IsSuccessStatusCode)
            {
                sb.AppendLine($"| {part.Id} | {part.BuildType?.Name ?? "—"} | #{part.Number} | {part.Status} | ⚠️ failed to load totals ({(int)totalsResponse.StatusCode}) | | | | |");
                continue;
            }

            var totalsJson = await totalsResponse.Content.ReadAsStringAsync();
            var totalsEnvelope = JsonSerializer.Deserialize(totalsJson, TeamCityJsonContext.Default.TestOccurrenceTotalsEnvelope);
            var t = totalsEnvelope?.TestOccurrences;

            sb.AppendLine($"| {part.Id} | {part.BuildType?.Name ?? "—"} | #{part.Number} | {part.Status} | {t?.Passed ?? 0} | {t?.Failed ?? 0} | {t?.Ignored ?? 0} | {t?.Muted ?? 0} | {t?.NewFailed ?? 0} |");
        }

        if (parts.Count > MaxChainPartsRendered)
            sb.AppendLine($"\n*({parts.Count - MaxChainPartsRendered} additional sub-build(s) not shown.)*");

        sb.AppendLine();
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
