namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_list_projects"),
        Description(
            "Lists all projects in the TeamCity instance with their parent project relationships. " +
            "Returns a markdown table with columns: ID, Name, Parent Project ID, Description.")]
    public async Task<string> ListProjects()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "project(id,name,parentProjectId,description)";
            var url = $"app/rest/projects?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var projectList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ProjectListResponse);

            var projects = projectList?.Project?
                .Where(p => p.Id != "_Root")
                .ToList();

            if (projects is null || projects.Count == 0)
                return "No projects found.";

            var sb = new StringBuilder();
            sb.AppendLine("# Projects");
            sb.AppendLine();
            sb.AppendLine($"**Total:** {projects.Count}");
            sb.AppendLine();
            sb.AppendLine("| ID | Name | Parent Project ID | Description |");
            sb.AppendLine("|----|------|-------------------|-------------|");

            foreach (var project in projects)
            {
                var desc = string.IsNullOrWhiteSpace(project.Description) ? "—" : project.Description;
                sb.AppendLine($"| {project.Id} | {project.Name} | {project.ParentProjectId ?? "—"} | {desc} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list projects — {ex.Message}";
        }
    }
}
