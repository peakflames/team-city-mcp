namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_list_build_types"),
        Description(
            "Lists TeamCity build configurations (build types). Optionally scoped to a specific project. " +
            "Returns a markdown table with columns: ID, Name, Project ID, Project Name.")]
    public async Task<string> ListBuildTypes(
        [Description("Optional project ID to scope the results. When omitted, all build types across all projects are returned.")]
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
            var fields = "buildType(id,name,projectId,projectName)";
            string url;
            if (string.IsNullOrWhiteSpace(projectId))
                url = $"app/rest/buildTypes?fields={Uri.EscapeDataString(fields)}";
            else
                url = $"app/rest/projects/id:{projectId}/buildTypes?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return string.IsNullOrWhiteSpace(projectId)
                    ? "ERROR: Build types endpoint was not found."
                    : $"ERROR: Project with ID '{projectId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var buildTypeList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeListResponse);

            if (buildTypeList?.BuildType is null || buildTypeList.BuildType.Count == 0)
            {
                return string.IsNullOrWhiteSpace(projectId)
                    ? "No build types found."
                    : $"No build types found for project '{projectId}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Build Configurations");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            sb.AppendLine($"**Total:** {buildTypeList.BuildType.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Name | Project ID | Project Name |");
            sb.AppendLine("|----|------|------------|--------------|");

            foreach (var bt in buildTypeList.BuildType)
                sb.AppendLine($"| {bt.Id} | {bt.Name} | {bt.ProjectId} | {bt.ProjectName} |");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list build types — {ex.Message}";
        }
    }
}
