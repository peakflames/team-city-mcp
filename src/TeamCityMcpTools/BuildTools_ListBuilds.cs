namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "list_builds"),
        Description(
            "Lists recent builds for a TeamCity build type, with optional filters for branch, " +
            "status, and count. Returns a markdown table with columns: ID, Number, Status, Branch, Started, Finished, URL.")]
    public async Task<string> ListBuilds(
        [Description("The TeamCity build type ID (e.g., 'MyProject_Build').")]
        string buildTypeId,

        [Description("Optional branch name to filter builds by.")]
        string? branch = null,

        [Description("Optional build status filter: SUCCESS, FAILURE, or ERROR.")]
        string? status = null,

        [Description("Maximum number of builds to return. Defaults to 10.")]
        int count = 10)
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
                $"buildType:id:{buildTypeId}",
                $"count:{count}",
                "state:finished"
            };
            if (!string.IsNullOrWhiteSpace(branch))
                locatorParts.Add($"branch:{branch}");
            if (!string.IsNullOrWhiteSpace(status))
                locatorParts.Add($"status:{status.ToUpperInvariant()}");

            var locator = string.Join(",", locatorParts);
            var fields = "build(id,number,status,state,branchName,startDate,finishDate,webUrl)";
            var url = $"app/rest/builds?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var buildList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildListResponse);

            if (buildList?.Build is null || buildList.Build.Count == 0)
                return $"No builds found for build type '{buildTypeId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Builds");
            sb.AppendLine();
            sb.AppendLine($"**Build Type:** {buildTypeId}");
            if (!string.IsNullOrWhiteSpace(branch))
                sb.AppendLine($"**Branch Filter:** {branch}");
            if (!string.IsNullOrWhiteSpace(status))
                sb.AppendLine($"**Status Filter:** {status}");
            sb.AppendLine($"**Count:** {buildList.Build.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Number | Status | Branch | Started | Finished | URL |");
            sb.AppendLine("|-----|--------|--------|--------|---------|----------|-----|");

            foreach (var build in buildList.Build)
            {
                var started  = FormatTcDate(build.StartDate);
                var finished = FormatTcDate(build.FinishDate);
                sb.AppendLine($"| {build.Id} | {build.Number} | {build.Status} | {build.BranchName ?? "default"} | {started} | {finished} | {build.WebUrl} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list builds — {ex.Message}";
        }
    }

    // TeamCity date format: 20241119T102304+0000
    private static string FormatTcDate(string? tcDate)
    {
        if (string.IsNullOrWhiteSpace(tcDate) || tcDate.Length < 15)
            return tcDate ?? "—";
        return $"{tcDate[..4]}-{tcDate[4..6]}-{tcDate[6..8]} {tcDate[9..11]}:{tcDate[11..13]}";
    }
}
