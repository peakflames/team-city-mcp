namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_build_type_dependency_graph"),
        Description(
            "Renders the design-time snapshot-dependency configuration graph for a build type — " +
            "distinct from any actual build run. Shows forward Dependencies (what this build type " +
            "is configured to depend on) and/or reverse Dependents (what depends on it).")]
    public async Task<string> GetBuildTypeDependencyGraph(
        [Description("The TeamCity build type ID (e.g., 'MyProject_Build').")]
        string buildTypeId,

        [Description("Which relationships to render: 'dependencies', 'dependents', or 'both' (default).")]
        string only = "both",

        [Description("Maximum depth to walk. Defaults to 10.")]
        int depth = 10)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;
        var showDependencies = !string.Equals(only, "dependents", StringComparison.OrdinalIgnoreCase);
        var showDependents = !string.Equals(only, "dependencies", StringComparison.OrdinalIgnoreCase);

        try
        {
            var rootFields = "id,name,projectId,projectName";
            var rootUrl = $"app/rest/buildTypes/id:{Uri.EscapeDataString(buildTypeId)}?fields={Uri.EscapeDataString(rootFields)}";
            var rootResponse = await client.HttpClient.GetAsync(rootUrl);

            if (rootResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (rootResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build type with ID '{buildTypeId}' was not found.";
            if (!rootResponse.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)rootResponse.StatusCode}: {rootResponse.ReasonPhrase}";

            var rootJson = await rootResponse.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize(rootJson, TeamCityJsonContext.Default.BuildTypeSummary);

            if (root is null)
                return $"ERROR: Unable to parse build type details for '{buildTypeId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Type Dependency Graph");
            sb.AppendLine();
            sb.AppendLine($"**Build Type:** {root.ProjectName} / {root.Name} (`{root.Id}`)");
            sb.AppendLine($"**Max Depth:** {depth}");
            sb.AppendLine();

            if (showDependencies)
            {
                sb.AppendLine("## Dependencies (what this depends on)");
                sb.AppendLine();
                sb.AppendLine($"- **{root.Name}** (`{root.Id}`)");
                var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id! };
                await RenderForwardDependencies(client, sb, root.Id!, 1, depth, visited);
                sb.AppendLine();
            }

            if (showDependents)
            {
                sb.AppendLine("## Dependents (what depends on this)");
                sb.AppendLine();
                sb.AppendLine($"- **{root.Name}** (`{root.Id}`)");
                var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id! };
                await RenderReverseDependents(client, sb, root.Id!, 1, depth, visited);
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build type dependency graph — {ex.Message}";
        }
    }

    private static async Task RenderForwardDependencies(
        TeamCityClient client,
        StringBuilder sb,
        string buildTypeId,
        int currentDepth,
        int maxDepth,
        HashSet<string> visited)
    {
        if (currentDepth > maxDepth)
            return;

        var fields = "count,snapshot-dependency(id,source-buildType(id,name,projectName))";
        var url = $"app/rest/buildTypes/id:{Uri.EscapeDataString(buildTypeId)}/snapshot-dependencies?fields={Uri.EscapeDataString(fields)}";

        var response = await client.HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var indent = new string(' ', currentDepth * 2);
            sb.AppendLine($"{indent}- ⚠️ Failed to load dependencies ({(int)response.StatusCode}: {response.ReasonPhrase})");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.SnapshotDependenciesWrapper);

        if (list?.SnapshotDependency is null || list.SnapshotDependency.Count == 0)
            return;

        foreach (var dependency in list.SnapshotDependency)
        {
            var source = dependency.SourceBuildType;
            if (source?.Id is null)
                continue;

            var indent = new string(' ', currentDepth * 2);
            var cyclic = visited.Contains(source.Id);
            var note = cyclic ? " *(cycle — already visited)*" : string.Empty;
            sb.AppendLine($"{indent}- **{source.Name}** (`{source.Id}`){(string.IsNullOrWhiteSpace(source.ProjectName) ? string.Empty : $" — {source.ProjectName}")}{note}");

            if (cyclic)
                continue;

            visited.Add(source.Id);
            await RenderForwardDependencies(client, sb, source.Id, currentDepth + 1, maxDepth, visited);
        }
    }

    private static async Task RenderReverseDependents(
        TeamCityClient client,
        StringBuilder sb,
        string buildTypeId,
        int currentDepth,
        int maxDepth,
        HashSet<string> visited)
    {
        if (currentDepth > maxDepth)
            return;

        var locator = $"snapshotDependency:(from:(id:{buildTypeId}),recursive:false)";
        var fields = "buildType(id,name,projectName)";
        var url = $"app/rest/buildTypes?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

        var response = await client.HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var indent = new string(' ', currentDepth * 2);
            sb.AppendLine($"{indent}- ⚠️ Failed to load dependents ({(int)response.StatusCode}: {response.ReasonPhrase})");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeListResponse);

        if (list?.BuildType is null || list.BuildType.Count == 0)
            return;

        foreach (var dependent in list.BuildType)
        {
            if (dependent.Id is null)
                continue;

            var indent = new string(' ', currentDepth * 2);
            var cyclic = visited.Contains(dependent.Id);
            var note = cyclic ? " *(cycle — already visited)*" : string.Empty;
            sb.AppendLine($"{indent}- **{dependent.Name}** (`{dependent.Id}`){(string.IsNullOrWhiteSpace(dependent.ProjectName) ? string.Empty : $" — {dependent.ProjectName}")}{note}");

            if (cyclic)
                continue;

            visited.Add(dependent.Id);
            await RenderReverseDependents(client, sb, dependent.Id, currentDepth + 1, maxDepth, visited);
        }
    }
}
