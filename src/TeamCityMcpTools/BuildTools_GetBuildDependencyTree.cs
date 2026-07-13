namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_dependency_tree"),
        Description(
            "Walks a build's actual snapshot-dependency chain and renders an indented markdown tree " +
            "with per-node status. Use direction 'down' (default) to see what this build depends on, " +
            "or 'up' to see what depends on this build. Helps diagnose whether dependencies were reused " +
            "or rebuilt for a given run.")]
    public async Task<string> GetBuildDependencyTree(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Maximum depth to walk. Defaults to 10.")]
        int depth = 10,

        [Description("Direction to walk: 'down' for dependencies (default), 'up' for dependents.")]
        string direction = "down")
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;
        var up = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);

        try
        {
            var rootFields = "id,number,status,state,buildTypeId,buildType(id,name)";
            var rootUrl = $"app/rest/builds/id:{buildId}?fields={Uri.EscapeDataString(rootFields)}";
            var rootResponse = await client.HttpClient.GetAsync(rootUrl);

            if (rootResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (rootResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build with ID '{buildId}' was not found.";
            if (!rootResponse.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)rootResponse.StatusCode}: {rootResponse.ReasonPhrase}";

            var rootJson = await rootResponse.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize(rootJson, TeamCityJsonContext.Default.DependencyBuildNode);

            if (root is null)
                return $"ERROR: Unable to parse build details for ID '{buildId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Dependency Tree");
            sb.AppendLine();
            sb.AppendLine($"**Direction:** {(up ? "dependents (up)" : "dependencies (down)")}");
            sb.AppendLine($"**Max Depth:** {depth}");
            sb.AppendLine();
            sb.AppendLine(FormatNodeLine(root.BuildType?.Name ?? root.BuildTypeId, root.Number, root.Id, root.Status, root.State, 0, null));

            var visited = new HashSet<int> { root.Id };

            await RenderDependencyChildren(client, sb, root.Id, 1, depth, up, visited);

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build dependency tree — {ex.Message}";
        }
    }

    private static async Task RenderDependencyChildren(
        TeamCityClient client,
        StringBuilder sb,
        int nodeId,
        int currentDepth,
        int maxDepth,
        bool up,
        HashSet<int> visited)
    {
        if (currentDepth > maxDepth)
            return;

        var directionLocator = up
            ? $"snapshotDependency:(from:(id:{nodeId}),recursive:false)"
            : $"snapshotDependency:(to:(id:{nodeId}),recursive:false)";
        var fields = "build(id,number,status,state,buildTypeId,buildType(id,name))";
        var url = $"app/rest/builds?locator={Uri.EscapeDataString(directionLocator)}&fields={Uri.EscapeDataString(fields)}";

        var response = await client.HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var indent = new string(' ', currentDepth * 2);
            sb.AppendLine($"{indent}- ⚠️ Failed to load children ({(int)response.StatusCode}: {response.ReasonPhrase})");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.DependencyBuildListResponse);

        if (list?.Build is null || list.Build.Count == 0)
            return;

        foreach (var node in list.Build)
        {
            var cyclic = visited.Contains(node.Id);
            sb.AppendLine(FormatNodeLine(node.BuildType?.Name ?? node.BuildTypeId, node.Number, node.Id, node.Status, node.State, currentDepth, cyclic ? "cycle — already visited" : null));

            if (cyclic)
                continue;

            visited.Add(node.Id);
            await RenderDependencyChildren(client, sb, node.Id, currentDepth + 1, maxDepth, up, visited);
        }
    }

    private static string FormatNodeLine(string? name, string? number, int id, string? status, string? state, int currentDepth, string? note)
    {
        var indent = new string(' ', currentDepth * 2);
        var suffix = note is null ? string.Empty : $" *({note})*";
        return $"{indent}- {StatusIcon(status, state)} **{name}** #{number} (id:{id}, {status}/{state}){suffix}";
    }

    private static string StatusIcon(string? status, string? state)
    {
        if (string.Equals(state, "running", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "queued", StringComparison.OrdinalIgnoreCase))
            return "⏳";

        return status?.ToUpperInvariant() switch
        {
            "SUCCESS" => "✅",
            "FAILURE" => "❌",
            "ERROR" => "❌",
            _ => "❔"
        };
    }
}
