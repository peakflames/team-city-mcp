namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.ListTemplates),
        Description(
            "Lists TeamCity build templates. Optionally scoped to a specific project. A template is a build type " +
            "with templateFlag set — use 'teamcity_get_build_type' with a template's ID to see its full detail " +
            "(settings, VCS roots, triggers, steps, dependencies), since that tool already resolves template IDs. " +
            "Optional 'nameFilter' matches a case-insensitive regex against the template Name only, and optional " +
            "'idFilter' matches a case-insensitive regex against the template ID only. " +
            "Results are capped at 'count' (default 100) — narrow with 'nameFilter'/'idFilter' or raise 'count' to see more. " +
            "Returns a markdown table with columns: ID, Name, Project ID, Project Name.")]
    public async Task<string> ListTemplates(
        [Description("Optional project ID to scope the results. When omitted, all templates across all projects are returned.")]
        string? projectId = null,

        [Description("Optional case-insensitive regex matched against the template Name only (e.g. '^MyProject$' for an exact match, 'foo|bar' for alternation).")]
        string? nameFilter = null,

        [Description("Optional case-insensitive regex matched against the template ID — e.g. to enumerate a subtree by ID prefix like '^Some_Parent_'.")]
        string? idFilter = null,

        [Description("Maximum number of matching templates to display. Defaults to 100.")]
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

            var nameRx = TeamCityFormat.CompileFilter(nameFilter);
            if (nameRx.IsFailed) return $"ERROR: Invalid nameFilter regex — {nameRx.Errors.First().Message}";
            var idRx = TeamCityFormat.CompileFilter(idFilter);
            if (idRx.IsFailed) return $"ERROR: Invalid idFilter regex — {idRx.Errors.First().Message}";

            var filtered = templateList.BuildType.Where(t =>
                (nameRx.Value is null || nameRx.Value.IsMatch(t.Name ?? "")) &&
                (idRx.Value is null || idRx.Value.IsMatch(t.Id ?? ""))).ToList();

            var ordered = filtered.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Build Templates");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            if (!string.IsNullOrWhiteSpace(nameFilter))
                sb.AppendLine($"**Name Filter:** {nameFilter}");
            if (!string.IsNullOrWhiteSpace(idFilter))
                sb.AppendLine($"**ID Filter:** {idFilter}");
            sb.AppendLine($"**Total:** {ordered.Count}");
            sb.AppendLine();

            if (displayed.Count == 0)
            {
                sb.AppendLine($"No templates matched nameFilter '{nameFilter}' / idFilter '{idFilter}'.");
                return sb.ToString();
            }

            sb.AppendLine("| ID | Name | Project ID | Project Name |");
            sb.AppendLine("|----|------|------------|--------------|");

            foreach (var template in displayed)
                sb.AppendLine($"| {template.Id} | {template.Name} | {template.ProjectId} | {template.ProjectName} |");

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more templates not shown — narrow with 'nameFilter' or raise 'count'.*");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list templates — {ex.Message}";
        }
    }
}
