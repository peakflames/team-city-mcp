namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetProjectHierarchy),
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

            var byId = projectList.Project
                .Where(p => p.Id is not null)
                .ToDictionary(p => p.Id!, p => p, StringComparer.Ordinal);

            var gate = scope.ServiceProvider.GetRequiredService<IPermissionGate>();
            var callContext = scope.ServiceProvider.GetRequiredService<IRbacToolCallContext>();
            VisibleProjectSet? visibleSet = null;

            if (gate.Enabled && callContext.CurrentIdentity is { } identity)
                visibleSet = await gate.GetVisibleProjectSetAsync(TeamCityToolNames.GetProjectHierarchy, identity);

            bool IsVisible(string? id) => visibleSet is null || visibleSet.IsGlobal || visibleSet.Contains(id);

            // TeamCity permissions inherit downward, but a role can also be granted directly on a
            // child — an invisible parent with a visible child is real, so an invisible ancestor
            // must never prune its visible descendants. Instead, each visible project is re-rooted
            // at the nearest visible ancestor (walking the *original*, unfiltered chain), rather than
            // rendered under a placeholder: a placeholder would itself assert the hidden project's
            // existence, position, and child count — strictly worse than a silent re-root.
            string EffectiveParent(ProjectSummary p)
            {
                var candidate = p.ParentProjectId ?? "_Root";
                while (candidate != "_Root" && !IsVisible(candidate))
                {
                    candidate = byId.TryGetValue(candidate, out var parent) ? parent.ParentProjectId ?? "_Root" : "_Root";
                }
                return candidate;
            }

            var visibleProjects = projectList.Project.Where(p => p.Id != "_Root" && IsVisible(p.Id)).ToList();

            if (visibleSet is not null && !visibleSet.IsGlobal)
            {
                var totalCount = projectList.Project.Count(p => p.Id != "_Root");
                if (visibleProjects.Count < totalCount)
                    callContext.ReportFilteredOut(totalCount - visibleProjects.Count);
            }

            // Build parent → children map over the re-rooted tree.
            var childrenOf = new Dictionary<string, List<ProjectSummary>>(StringComparer.Ordinal);
            foreach (var p in visibleProjects)
            {
                var parentId = EffectiveParent(p);
                if (!childrenOf.TryGetValue(parentId, out var list))
                {
                    list = [];
                    childrenOf[parentId] = list;
                }
                list.Add(p);
            }

            var startId = string.IsNullOrWhiteSpace(projectId) ? "_Root" : projectId;

            // An invisible startId (whether it doesn't exist at all, or exists but the caller can't
            // see it) must render the byte-identical not-found string — never ToolGate.DeniedMessage,
            // and never distinguishable from genuine absence.
            if (startId != "_Root" && !visibleProjects.Any(p => string.Equals(p.Id, startId, StringComparison.Ordinal)))
                return $"ERROR: Project with ID '{startId}' was not found.";

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
