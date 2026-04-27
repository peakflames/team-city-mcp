namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_project_hierarchy"),
        Description(
            "Renders the TeamCity project tree as an indented markdown list showing parent/child nesting. " +
            "Optionally start from a specific project ID (defaults to root). " +
            "Optionally limit nesting depth (0 = unlimited).")]
    public async Task<string> GetProjectHierarchy(
        [Description("Optional project ID to use as the root of the tree. Defaults to the instance root.")]
        string? projectId = null,

        [Description("Maximum nesting depth to render. 0 means unlimited. Defaults to 0.")]
        int depth = 0)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "project(id,name,parentProjectId)";
            var url = $"app/rest/projects?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var projectList = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ProjectListResponse);

            if (projectList?.Project is null || projectList.Project.Count == 0)
                return "No projects found.";

            // Build parent → children map
            var childrenOf = new Dictionary<string, List<ProjectSummary>>(StringComparer.Ordinal);
            foreach (var p in projectList.Project)
            {
                var parentId = p.ParentProjectId ?? "_Root";
                if (!childrenOf.TryGetValue(parentId, out var list))
                {
                    list = [];
                    childrenOf[parentId] = list;
                }
                list.Add(p);
            }

            var startId = string.IsNullOrWhiteSpace(projectId) ? "_Root" : projectId;

            if (startId != "_Root" && !childrenOf.ContainsKey(startId))
            {
                // Check if the requested project exists at all
                var exists = projectList.Project.Any(p => string.Equals(p.Id, startId, StringComparison.Ordinal));
                if (!exists)
                    return $"ERROR: Project with ID '{startId}' was not found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Project Hierarchy");
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"*Rooted at: {projectId}*");
            sb.AppendLine();

            void RenderChildren(string parentId, int currentDepth)
            {
                if (!childrenOf.TryGetValue(parentId, out var children))
                    return;

                foreach (var child in children)
                {
                    var indent = new string(' ', currentDepth * 2);
                    sb.AppendLine($"{indent}- **{child.Name}** (`{child.Id}`)");

                    if (depth == 0 || currentDepth + 1 < depth)
                        RenderChildren(child.Id!, currentDepth + 1);
                }
            }

            RenderChildren(startId, 0);

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get project hierarchy — {ex.Message}";
        }
    }
}
