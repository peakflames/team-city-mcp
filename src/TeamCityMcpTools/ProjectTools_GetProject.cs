namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_project"),
        Description(
            "Gets details for a specific TeamCity project by ID, including its child projects and build configurations. " +
            "Returns a markdown document with project metadata, a child projects table, and a build types table.")]
    public async Task<string> GetProject(
        [Description("The TeamCity project ID (e.g., 'MyProject').")]
        string projectId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "id,name,description,parentProject(id,name),projects(project(id,name)),buildTypes(buildType(id,name))";
            var url = $"app/rest/projects/id:{projectId}?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Project with ID '{projectId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var project = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ProjectDetails);

            if (project is null)
                return $"ERROR: Unable to parse project details for ID '{projectId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Project: {project.Name}");
            sb.AppendLine();
            sb.AppendLine("## Details");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| ID | {project.Id} |");
            sb.AppendLine($"| Name | {project.Name} |");
            sb.AppendLine($"| Description | {(string.IsNullOrWhiteSpace(project.Description) ? "—" : project.Description)} |");
            sb.AppendLine($"| Parent Project | {project.ParentProject?.Name ?? "—"} ({project.ParentProject?.Id ?? "—"}) |");
            sb.AppendLine();

            var childProjects = project.Projects?.Project;
            if (childProjects is { Count: > 0 })
            {
                sb.AppendLine("## Child Projects");
                sb.AppendLine();
                sb.AppendLine("| ID | Name |");
                sb.AppendLine("|----|------|");
                foreach (var child in childProjects)
                    sb.AppendLine($"| {child.Id} | {child.Name} |");
                sb.AppendLine();
            }

            var buildTypes = project.BuildTypes?.BuildType;
            if (buildTypes is { Count: > 0 })
            {
                sb.AppendLine("## Build Configurations");
                sb.AppendLine();
                sb.AppendLine("| ID | Name |");
                sb.AppendLine("|----|------|");
                foreach (var bt in buildTypes)
                    sb.AppendLine($"| {bt.Id} | {bt.Name} |");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get project — {ex.Message}";
        }
    }
}
