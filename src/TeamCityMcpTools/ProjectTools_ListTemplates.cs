namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_list_templates"),
        Description(
            "Lists TeamCity build templates. Optionally scoped to a specific project. A template is a build type " +
            "with templateFlag set — use 'teamcity_get_build_type' with a template's ID to see its full detail " +
            "(settings, VCS roots, triggers, steps, dependencies), since that tool already resolves template IDs. " +
            "Returns a markdown table with columns: ID, Name, Project ID, Project Name.")]
    public async Task<string> ListTemplates(
        [Description("Optional project ID to scope the results. When omitted, all templates across all projects are returned.")]
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
            var fields = "count,buildType(id,name,projectId,projectName)";
            string url;
            if (string.IsNullOrWhiteSpace(projectId))
                url = $"app/rest/buildTypes?locator=templateFlag:true&fields={Uri.EscapeDataString(fields)}";
            else
                url = $"app/rest/projects/id:{projectId}/templates?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return string.IsNullOrWhiteSpace(projectId)
                    ? "ERROR: Templates endpoint was not found."
                    : $"ERROR: Project with ID '{projectId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var templateList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeListResponse);

            if (templateList?.BuildType is null || templateList.BuildType.Count == 0)
            {
                return string.IsNullOrWhiteSpace(projectId)
                    ? "No templates found."
                    : $"No templates found for project '{projectId}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Build Templates");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            sb.AppendLine($"**Total:** {templateList.BuildType.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Name | Project ID | Project Name |");
            sb.AppendLine("|----|------|------------|--------------|");

            foreach (var template in templateList.BuildType)
                sb.AppendLine($"| {template.Id} | {template.Name} | {template.ProjectId} | {template.ProjectName} |");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list templates — {ex.Message}";
        }
    }
}
