namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_dependency_tree"),
        Description(
            "Walks a build's actual dependency chain — both snapshot and artifact dependencies — and " +
            "renders an indented markdown tree with per-node status, labeled [snapshot], [artifact], or " +
            "[snapshot+artifact]. Use direction 'down' (default) to see what this build depends on, " +
            "or 'up' to see what depends on this build (snapshot dependents only — artifact dependents " +
            "cannot be resolved via the TeamCity API). Note: TeamCity's run-level artifact-dependencies " +
            "field is often unpopulated even for a resolved, successful artifact dependency — if a build " +
            "shows no artifact deps here, cross-check teamcity_get_build_type or " +
            "teamcity_get_build_type_dependency_graph, which read the design-time configuration instead. " +
            "Helps diagnose whether dependencies were reused or rebuilt for a given run.")]
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
            if (up)
            {
                sb.AppendLine("*Note: only snapshot dependents are shown — TeamCity has no reverse locator for artifact dependencies.*");
            }
            else
            {
                sb.AppendLine("*Note: TeamCity's run-level artifact-dependencies field is often unpopulated even for a resolved, " +
                    "successful artifact dependency. If an expected artifact dependency is missing below, check " +
                    "teamcity_get_build_type or teamcity_get_build_type_dependency_graph for the configured dependency.*");
            }
            sb.AppendLine();
            sb.AppendLine(FormatNodeLine(root.BuildType?.Name ?? root.BuildTypeId, root.Number, root.Id, root.Status, root.State, 0, null, null));

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

        if (up)
        {
            var directionLocator = $"snapshotDependency:(from:(id:{nodeId}),recursive:false)";
            var dependentsFields = "build(id,number,status,state,buildTypeId,buildType(id,name))";
            var dependentsUrl = $"app/rest/builds?locator={Uri.EscapeDataString(directionLocator)}&fields={Uri.EscapeDataString(dependentsFields)}";

            var dependentsResponse = await client.HttpClient.GetAsync(dependentsUrl);
            if (!dependentsResponse.IsSuccessStatusCode)
            {
                var indent = new string(' ', currentDepth * 2);
                sb.AppendLine($"{indent}- ⚠️ Failed to load children ({(int)dependentsResponse.StatusCode}: {dependentsResponse.ReasonPhrase})");
                return;
            }

            var dependentsJson = await dependentsResponse.Content.ReadAsStringAsync();
            var dependentsList = JsonSerializer.Deserialize(dependentsJson, TeamCityJsonContext.Default.DependencyBuildListResponse);

            if (dependentsList?.Build is null || dependentsList.Build.Count == 0)
                return;

            foreach (var node in dependentsList.Build)
            {
                var cyclic = visited.Contains(node.Id);
                sb.AppendLine(FormatNodeLine(node.BuildType?.Name ?? node.BuildTypeId, node.Number, node.Id, node.Status, node.State, currentDepth, cyclic ? "cycle — already visited" : null, "snapshot"));

                if (cyclic)
                    continue;

                visited.Add(node.Id);
                await RenderDependencyChildren(client, sb, node.Id, currentDepth + 1, maxDepth, up, visited);
            }

            return;
        }

        var fields = "snapshot-dependencies(build(id,number,status,state,buildTypeId,buildType(id,name))),artifact-dependencies(build(id,number,status,state,buildTypeId,buildType(id,name)))";
        var url = $"app/rest/builds/id:{nodeId}?fields={Uri.EscapeDataString(fields)}";

        var response = await client.HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var indent = new string(' ', currentDepth * 2);
            sb.AppendLine($"{indent}- ⚠️ Failed to load children ({(int)response.StatusCode}: {response.ReasonPhrase})");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildDependenciesEnvelope);

        var merged = new Dictionary<int, (DependencyBuildNode Node, bool Snapshot, bool Artifact)>();

        foreach (var node in envelope?.SnapshotDependencies?.Build ?? [])
        {
            merged[node.Id] = merged.TryGetValue(node.Id, out var existing)
                ? (node, true, existing.Artifact)
                : (node, true, false);
        }

        foreach (var node in envelope?.ArtifactDependencies?.Build ?? [])
        {
            merged[node.Id] = merged.TryGetValue(node.Id, out var existing)
                ? (node, existing.Snapshot, true)
                : (node, false, true);
        }

        if (merged.Count == 0)
            return;

        foreach (var (node, isSnapshot, isArtifact) in merged.Values)
        {
            var cyclic = visited.Contains(node.Id);
            var kind = isSnapshot && isArtifact ? "snapshot+artifact" : isSnapshot ? "snapshot" : "artifact";
            sb.AppendLine(FormatNodeLine(node.BuildType?.Name ?? node.BuildTypeId, node.Number, node.Id, node.Status, node.State, currentDepth, cyclic ? "cycle — already visited" : null, kind));

            if (cyclic)
                continue;

            visited.Add(node.Id);
            await RenderDependencyChildren(client, sb, node.Id, currentDepth + 1, maxDepth, up, visited);
        }
    }

    private static string FormatNodeLine(string? name, string? number, int id, string? status, string? state, int currentDepth, string? note, string? kind)
    {
        var indent = new string(' ', currentDepth * 2);
        var suffix = note is null ? string.Empty : $" *({note})*";
        var kindLabel = kind is null ? string.Empty : $" [{kind}]";
        return $"{indent}- {StatusIcon(status, state)} **{name}** #{number} (id:{id}, {status}/{state}){kindLabel}{suffix}";
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
