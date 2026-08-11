namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = TeamCityToolNames.GetBuildProblems),
        Description(
            "Gets dedicated build problem occurrences for a specific build (e.g. exit-code failures, " +
            "OOM, snapshot dependency failures) — distinct from test failures and from the problem " +
            "summary embedded in teamcity_get_build.")]
    public async Task<string> GetBuildProblems(
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
            var locator = $"build:(id:{buildId})";
            var fields = "count,problemOccurrence(id,type,identity,details)";
            var url = $"app/rest/problemOccurrences?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var problems = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildProblemsResponse);

            if (problems is null)
                return $"ERROR: Unable to parse build problems for build '{buildId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Problems");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Count:** {problems.Count ?? 0}");
            sb.AppendLine();

            if (problems.ProblemOccurrence is not { Count: > 0 } occurrences)
            {
                sb.AppendLine("No build problems found for this build.");
                return sb.ToString();
            }

            foreach (var problem in occurrences)
            {
                sb.AppendLine($"### {problem.Type ?? "UNKNOWN"}");
                sb.AppendLine();
                sb.AppendLine($"- **ID:** {problem.Id}");
                if (!string.IsNullOrWhiteSpace(problem.Identity))
                    sb.AppendLine($"- **Identity:** {problem.Identity}");
                if (!string.IsNullOrWhiteSpace(problem.Details))
                    sb.AppendLine($"- **Details:**\n  ```\n  {problem.Details.Replace("\n", "\n  ")}\n  ```");
                sb.AppendLine();
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build problems — {ex.Message}";
        }
    }
}
