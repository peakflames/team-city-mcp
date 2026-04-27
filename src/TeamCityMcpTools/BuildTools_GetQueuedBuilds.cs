namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_queued_builds"),
        Description(
            "Gets all builds currently waiting in the build queue, optionally filtered by project. " +
            "Returns a markdown table with columns: ID, Build Type, Project, Branch, Triggered By, Wait Reason, URL.")]
    public async Task<string> GetQueuedBuilds(
        [Description("Optional project ID to filter queued builds by project.")]
        string? projectId = null)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "build(id,buildType(id,name,projectName),branchName,triggered(type,user(name),date),waitReason,webUrl)";
            string url;
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                var locator = $"project:id:{projectId}";
                url = $"app/rest/buildQueue?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";
            }
            else
            {
                url = $"app/rest/buildQueue?fields={Uri.EscapeDataString(fields)}";
            }

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var queuedList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.QueuedBuildListResponse);

            if (queuedList?.Build is null || queuedList.Build.Count == 0)
                return "No builds in queue.";

            var sb = new StringBuilder();
            sb.AppendLine("# Queued Builds");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            sb.AppendLine($"**Count:** {queuedList.Build.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Build Type | Project | Branch | Triggered By | Wait Reason | URL |");
            sb.AppendLine("|----|------------|---------|--------|--------------|-------------|-----|");

            foreach (var build in queuedList.Build)
            {
                var buildTypeName = build.BuildType?.Name ?? "—";
                var projectName = build.BuildType?.ProjectName ?? "—";
                var branch = build.BranchName ?? "default";
                var triggeredBy = build.Triggered?.User?.Name ?? build.Triggered?.Type ?? "—";
                var waitReason = string.IsNullOrWhiteSpace(build.WaitReason) ? "—" : build.WaitReason;
                sb.AppendLine($"| {build.Id} | {buildTypeName} | {projectName} | {branch} | {triggeredBy} | {waitReason} | {build.WebUrl} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get queued builds — {ex.Message}";
        }
    }
}
