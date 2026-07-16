namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_list_build_types"),
        Description(
            "Lists TeamCity build configurations (build types). Optionally scoped to a specific project. " +
            "Optional 'nameFilter' matches a case-insensitive regex against the build type Name only, and optional " +
            "'idFilter' matches a case-insensitive regex against the build type ID only. " +
            "Results are capped at 'count' (default 100) — narrow with 'nameFilter'/'idFilter' or raise 'count' to see more. " +
            "Returns a markdown table with columns: ID, Name, Project ID, Project Name.")]
    public async Task<string> ListBuildTypes(
        [Description("Optional project ID to scope the results. When omitted, all build types across all projects are returned.")]
        string? projectId = null,

        [Description("Optional case-insensitive regex matched against the build type Name only (e.g. '^MyProject$' for an exact match, 'foo|bar' for alternation).")]
        string? nameFilter = null,

        [Description("Optional case-insensitive regex matched against the build type ID — e.g. to enumerate a subtree by ID prefix like '^Some_Parent_'.")]
        string? idFilter = null,

        [Description("Maximum number of matching build types to display. Defaults to 100.")]
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

            var nameRx = TeamCityFormat.CompileFilter(nameFilter);
            if (nameRx.IsFailed) return $"ERROR: Invalid nameFilter regex — {nameRx.Errors.First().Message}";
            var idRx = TeamCityFormat.CompileFilter(idFilter);
            if (idRx.IsFailed) return $"ERROR: Invalid idFilter regex — {idRx.Errors.First().Message}";

            var filtered = buildTypeList.BuildType.Where(bt =>
                (nameRx.Value is null || nameRx.Value.IsMatch(bt.Name ?? "")) &&
                (idRx.Value is null || idRx.Value.IsMatch(bt.Id ?? ""))).ToList();

            var ordered = filtered.OrderBy(bt => bt.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Build Configurations");
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
                sb.AppendLine($"No build types matched nameFilter '{nameFilter}' / idFilter '{idFilter}'.");
                return sb.ToString();
            }

            sb.AppendLine("| ID | Name | Project ID | Project Name |");
            sb.AppendLine("|----|------|------------|--------------|");

            foreach (var bt in displayed)
                sb.AppendLine($"| {bt.Id} | {bt.Name} | {bt.ProjectId} | {bt.ProjectName} |");

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more build types not shown — narrow with 'nameFilter' or raise 'count'.*");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list build types — {ex.Message}";
        }
    }
}
