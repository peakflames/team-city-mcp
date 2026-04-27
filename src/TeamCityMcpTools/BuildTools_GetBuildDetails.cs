namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build"),
        Description(
            "Gets comprehensive details for a specific TeamCity build, including status, agent, " +
            "VCS revisions, and build problems. Returns a markdown document mixed with XML tags.")]
    public async Task<string> GetBuild(
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
            var fields = "id,number,status,state,branchName,startDate,finishDate,duration,webUrl," +
                         "buildType(name,projectName)," +
                         "agent(name)," +
                         "triggered(user(name),date,type)," +
                         "revisions(revision(version,vcsBranch))," +
                         "problemOccurrences(count,problemOccurrence(type,details))";
            var url = $"app/rest/builds/id:{buildId}?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var build = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildDetails);

            if (build is null)
                return $"ERROR: Unable to parse build details for ID '{buildId}'.";

            var sb = new StringBuilder();

            sb.AppendLine("# Build Details");
            sb.AppendLine();
            sb.AppendLine($"<BUILD_METADATA id='{build.Id}' number='{build.Number}' status='{build.Status}' state='{build.State}' branch='{build.BranchName ?? "default"}'>");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| Build Type | {build.BuildType?.ProjectName} / {build.BuildType?.Name} |");
            sb.AppendLine($"| Number | {build.Number} |");
            sb.AppendLine($"| Status | {build.Status} |");
            sb.AppendLine($"| State | {build.State} |");
            sb.AppendLine($"| Branch | {build.BranchName ?? "default"} |");
            sb.AppendLine($"| Started | {FormatTcDate(build.StartDate)} |");
            sb.AppendLine($"| Finished | {FormatTcDate(build.FinishDate)} |");

            if (build.Duration.HasValue)
            {
                var dur = TimeSpan.FromSeconds(build.Duration.Value);
                sb.AppendLine($"| Duration | {(int)dur.TotalMinutes:D2}:{dur.Seconds:D2} |");
            }

            sb.AppendLine($"| Agent | {build.Agent?.Name ?? "unknown"} |");

            if (build.Triggered is not null)
            {
                var triggeredBy = build.Triggered.User?.Name ?? build.Triggered.Type ?? "unknown";
                sb.AppendLine($"| Triggered By | {triggeredBy} ({FormatTcDate(build.Triggered.Date)}) |");
            }

            sb.AppendLine($"| URL | {build.WebUrl} |");
            sb.AppendLine();
            sb.AppendLine("</BUILD_METADATA>");
            sb.AppendLine();

            if (build.Revisions?.Revision is { Count: > 0 } revisions)
            {
                sb.AppendLine("## VCS Revisions");
                sb.AppendLine();
                foreach (var rev in revisions)
                    sb.AppendLine($"- **{rev.VcsBranch ?? "unknown branch"}**: `{rev.Version}`");
                sb.AppendLine();
            }

            if (build.ProblemOccurrences?.Count > 0 &&
                build.ProblemOccurrences.ProblemOccurrence is { Count: > 0 } problems)
            {
                sb.AppendLine("## Build Problems");
                sb.AppendLine();
                foreach (var problem in problems)
                {
                    sb.AppendLine($"**Type:** {problem.Type}");
                    if (!string.IsNullOrWhiteSpace(problem.Details))
                        sb.AppendLine($"> {problem.Details}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build details — {ex.Message}";
        }
    }
}
