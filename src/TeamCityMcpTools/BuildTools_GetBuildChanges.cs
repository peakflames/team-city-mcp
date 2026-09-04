namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = TeamCityToolNames.GetBuildChanges),
        Description(
            "Gets the VCS changes (commits) included in a build, with author, comment, and changed " +
            "files — useful for finding what code change triggered or is included in a build.")]
    public async Task<string> GetBuildChanges(
        [Description("The TeamCity build ID (numeric).")]
        string buildId)
    {
        if (!TeamCityLocator.IsNumericId(buildId))
            return $"ERROR: Invalid buildId '{buildId}' — must be numeric.";

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var locator = $"build:(id:{buildId})";
            var fields = "count,change(id,version,username,date,comment,files(file(file,changeType)))";
            var url = $"app/rest/changes?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var changes = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ChangesResponse);

            if (changes is null)
                return $"ERROR: Unable to parse changes for build '{buildId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Changes");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Count:** {changes.Change?.Count ?? 0}");
            sb.AppendLine();

            if (changes.Change is not { Count: > 0 } changeList)
            {
                sb.AppendLine("No VCS changes found for this build.");
                return sb.ToString();
            }

            foreach (var change in changeList)
            {
                sb.AppendLine($"### {change.Version}");
                sb.AppendLine();
                sb.AppendLine($"- **Author:** {change.Username ?? "unknown"}");
                sb.AppendLine($"- **Date:** {TeamCityFormat.FormatTcDate(change.Date)}");
                if (!string.IsNullOrWhiteSpace(change.Comment))
                    sb.AppendLine($"- **Comment:** {change.Comment.Trim()}");

                if (change.Files?.File is { Count: > 0 } files)
                {
                    sb.AppendLine("- **Files:**");
                    foreach (var file in files)
                        sb.AppendLine($"  - `{file.FilePath}` ({file.ChangeType})");
                }

                sb.AppendLine();
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build changes — {ex.Message}";
        }
    }
}
