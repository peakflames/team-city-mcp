namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.ListProjects),
        Description(
            "Lists projects in the TeamCity instance with their parent project relationships. " +
            "Optional 'nameFilter' matches a case-insensitive regex against the project Name only, and optional " +
            "'idFilter' matches a case-insensitive regex against the project ID only. " +
            "Results are capped at 'count' (default 100) — narrow with 'nameFilter'/'idFilter' or raise 'count' to see more. " +
            "Returns a markdown table with columns: ID, Name, Parent Project ID, Description.")]
    public async Task<string> ListProjects(
        [Description("Optional case-insensitive regex matched against the project Name only (e.g. '^MyProject$' for an exact match, 'foo|bar' for alternation).")]
        string? nameFilter = null,

        [Description("Optional case-insensitive regex matched against the project ID — e.g. to enumerate a subtree by ID prefix like '^Some_Parent_'.")]
        string? idFilter = null,

        [Description("Maximum number of matching projects to display. Defaults to 100.")]
        int count = 100)
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

            // Resolved directly (not via ToolGate.FilterByVisibleSetAsync) because the parent-id
            // leak fix below needs the raw VisibleProjectSet, not just a filtered list.
            var gate = scope.ServiceProvider.GetRequiredService<IPermissionGate>();
            var callContext = scope.ServiceProvider.GetRequiredService<IRbacToolCallContext>();
            VisibleProjectSet? visibleSet = null;

            if (gate.Enabled && callContext.CurrentIdentity is { } identity)
            {
                visibleSet = await gate.GetVisibleProjectSetAsync(TeamCityToolNames.ListProjects, identity);
                if (!visibleSet.IsGlobal)
                {
                    var visibleCount = projects.Count(p => visibleSet.Contains(p.Id));
                    if (visibleCount < projects.Count)
                        callContext.ReportFilteredOut(projects.Count - visibleCount);
                    projects = projects.Where(p => visibleSet.Contains(p.Id)).ToList();
                }
            }

            var nameRx = TeamCityFormat.CompileFilter(nameFilter);
            if (nameRx.IsFailed) return $"ERROR: Invalid nameFilter regex — {nameRx.Errors.First().Message}";
            var idRx = TeamCityFormat.CompileFilter(idFilter);
            if (idRx.IsFailed) return $"ERROR: Invalid idFilter regex — {idRx.Errors.First().Message}";

            var filtered = projects.Where(p =>
                (nameRx.Value is null || nameRx.Value.IsMatch(p.Name ?? "")) &&
                (idRx.Value is null || idRx.Value.IsMatch(p.Id ?? ""))).ToList();

            var ordered = filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Projects");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(nameFilter))
                sb.AppendLine($"**Name Filter:** {nameFilter}");
            if (!string.IsNullOrWhiteSpace(idFilter))
                sb.AppendLine($"**ID Filter:** {idFilter}");
            sb.AppendLine($"**Total:** {ordered.Count}");
            sb.AppendLine();

            if (displayed.Count == 0)
            {
                sb.AppendLine($"No projects matched nameFilter '{nameFilter}' / idFilter '{idFilter}'.");
                return sb.ToString();
            }

            sb.AppendLine("| ID | Name | Parent Project ID | Description |");
            sb.AppendLine("|----|------|-------------------|-------------|");

            foreach (var project in displayed)
            {
                var desc = string.IsNullOrWhiteSpace(project.Description) ? "—" : project.Description;

                // A parent id is real data too — never render one the caller can't see, even though
                // the child itself is visible (a role can grant view_project directly on a child).
                var parentVisible = visibleSet is null || visibleSet.Contains(project.ParentProjectId);
                var parentId = parentVisible ? project.ParentProjectId ?? "—" : "—";

                sb.AppendLine($"| {project.Id} | {project.Name} | {parentId} | {desc} |");
            }

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more projects not shown — narrow with 'nameFilter' or raise 'count'.*");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list projects — {ex.Message}";
        }
    }
}
