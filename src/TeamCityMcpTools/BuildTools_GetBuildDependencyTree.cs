namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_get_build_dependency_tree"),
        Description(
            "Walks a build's actual dependency chain — both snapshot and artifact dependencies — and " +
            "renders an indented markdown tree (default) or a Mermaid graph (format:'mermaid'), with per-node " +
            "status, labeled [snapshot], [artifact], or [snapshot+artifact]. Use direction 'down' (default) to see " +
            "what this build depends on, or 'up' to see what depends on this build (snapshot dependents only — " +
            "artifact dependents cannot be resolved via the TeamCity API). Note: TeamCity's run-level " +
            "artifact-dependencies field is often unpopulated even for a resolved, successful artifact dependency " +
            "— if a build shows no artifact deps here, cross-check teamcity_get_build_type or " +
            "teamcity_get_build_type_dependency_graph, which read the design-time configuration instead. Helps " +
            "diagnose whether dependencies were reused or rebuilt for a given run. Orientation — matching the " +
            "TeamCity build-chain UI — dependencies (upstream) render left, dependents (downstream) render right, " +
            "with arrows always flowing dependency -> dependent.")]
    public async Task<string> GetBuildDependencyTree(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Maximum depth to walk. Defaults to 10.")]
        int depth = 10,

        [Description("Direction to walk: 'down' for dependencies (default), 'up' for dependents.")]
        string direction = "down",

        [Description("Output format: 'markdown' (default, indented tree) or 'mermaid' (fenced ```mermaid graph LR``` " +
                      "block; dependencies left, dependents right, arrows dependency -> dependent).")]
        string format = "markdown")
    {
        if (!string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "mermaid", StringComparison.OrdinalIgnoreCase))
            return $"ERROR: Unknown format '{format}'. Use 'markdown' or 'mermaid'.";

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;
        var up = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);
        var mermaid = string.Equals(format, "mermaid", StringComparison.OrdinalIgnoreCase);

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

            var rootNode = new DependencyGraphNode(root.Id, root.BuildType?.Name ?? root.BuildTypeId, root.Number, root.Status, root.State);

            var items = new List<DependencyGraphItem>();
            var visited = new HashSet<int> { root.Id };
            await CollectDependencyChildren(client, items, root.Id, 1, depth, up, visited);

            return mermaid
                ? RenderDependencyMermaid(rootNode, items, up, depth)
                : RenderDependencyMarkdown(rootNode, items, up, depth);
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build dependency tree — {ex.Message}";
        }
    }

    private static string RenderDependencyMarkdown(DependencyGraphNode root, List<DependencyGraphItem> items, bool up, int depth)
    {
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
        sb.AppendLine(FormatNodeLine(root.Name, root.Number, root.Id, root.Status, root.State, 0, null, null));

        foreach (var item in items)
        {
            switch (item)
            {
                case DependencyGraphNodeItem node:
                    sb.AppendLine(FormatNodeLine(node.Node.Name, node.Node.Number, node.Node.Id, node.Node.Status, node.Node.State,
                        node.Depth, node.Cyclic ? "cycle — already visited" : null, node.Kind));
                    break;
                case DependencyGraphWarningItem warning:
                    sb.AppendLine($"{new string(' ', warning.Depth * 2)}- ⚠️ {warning.Message}");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string RenderDependencyMermaid(DependencyGraphNode root, List<DependencyGraphItem> items, bool up, int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Build Dependency Tree");
        sb.AppendLine();
        sb.AppendLine($"**Direction:** {(up ? "dependents (up)" : "dependencies (down)")}");
        sb.AppendLine($"**Max Depth:** {depth}");
        sb.AppendLine();
        sb.AppendLine("*Orientation: dependencies (upstream) on the left, dependents (downstream) on the right — " +
                       "arrows flow dependency -> dependent.*");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");

        var declared = new HashSet<int>();

        void DeclareNode(DependencyGraphNode node, bool isRoot)
        {
            if (!declared.Add(node.Id))
                return;
            var id = TeamCityFormat.SanitizeMermaidId("b", node.Id.ToString());
            var label = TeamCityFormat.EscapeMermaidLabel($"{StatusIcon(node.Status, node.State)} {node.Name} #{node.Number} (id:{node.Id})");
            sb.AppendLine(isRoot ? $"    {id}[\"{label}\"]:::root" : $"    {id}[\"{label}\"]");
        }

        DeclareNode(root, true);
        foreach (var item in items)
        {
            if (item is DependencyGraphNodeItem node)
                DeclareNode(node.Node, false);
        }

        sb.AppendLine();

        var emittedEdges = new HashSet<(int Source, int Target)>();
        foreach (var item in items)
        {
            if (item is not DependencyGraphNodeItem node)
                continue;

            // Arrows always flow dependency -> dependent, regardless of which direction was walked.
            var (sourceId, targetId) = up ? (node.FromId, node.Node.Id) : (node.Node.Id, node.FromId);
            if (!emittedEdges.Add((sourceId, targetId)))
                continue;

            var sourceMermaidId = TeamCityFormat.SanitizeMermaidId("b", sourceId.ToString());
            var targetMermaidId = TeamCityFormat.SanitizeMermaidId("b", targetId.ToString());
            var label = node.Kind is null ? string.Empty : $"|{node.Kind}|";
            sb.AppendLine($"    {sourceMermaidId} -->{label} {targetMermaidId}");
        }

        sb.AppendLine();
        sb.AppendLine("    classDef root fill:#f9c74f,stroke:#333,stroke-width:2px;");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static async Task CollectDependencyChildren(
        TeamCityClient client,
        List<DependencyGraphItem> items,
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
                items.Add(new DependencyGraphWarningItem(currentDepth,
                    $"Failed to load children ({(int)dependentsResponse.StatusCode}: {dependentsResponse.ReasonPhrase})"));
                return;
            }

            var dependentsJson = await dependentsResponse.Content.ReadAsStringAsync();
            var dependentsList = JsonSerializer.Deserialize(dependentsJson, TeamCityJsonContext.Default.DependencyBuildListResponse);

            if (dependentsList?.Build is null || dependentsList.Build.Count == 0)
                return;

            foreach (var node in dependentsList.Build)
            {
                var cyclic = visited.Contains(node.Id);
                var graphNode = new DependencyGraphNode(node.Id, node.BuildType?.Name ?? node.BuildTypeId, node.Number, node.Status, node.State);
                items.Add(new DependencyGraphNodeItem(nodeId, graphNode, currentDepth, "snapshot", cyclic));

                if (cyclic)
                    continue;

                visited.Add(node.Id);
                await CollectDependencyChildren(client, items, node.Id, currentDepth + 1, maxDepth, up, visited);
            }

            return;
        }

        var fields = "snapshot-dependencies(build(id,number,status,state,buildTypeId,buildType(id,name))),artifact-dependencies(build(id,number,status,state,buildTypeId,buildType(id,name)))";
        var url = $"app/rest/builds/id:{nodeId}?fields={Uri.EscapeDataString(fields)}";

        var response = await client.HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            items.Add(new DependencyGraphWarningItem(currentDepth,
                $"Failed to load children ({(int)response.StatusCode}: {response.ReasonPhrase})"));
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
            var graphNode = new DependencyGraphNode(node.Id, node.BuildType?.Name ?? node.BuildTypeId, node.Number, node.Status, node.State);
            items.Add(new DependencyGraphNodeItem(nodeId, graphNode, currentDepth, kind, cyclic));

            if (cyclic)
                continue;

            visited.Add(node.Id);
            await CollectDependencyChildren(client, items, node.Id, currentDepth + 1, maxDepth, up, visited);
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

    private sealed record DependencyGraphNode(int Id, string? Name, string? Number, string? Status, string? State);

    private abstract record DependencyGraphItem;

    private sealed record DependencyGraphNodeItem(int FromId, DependencyGraphNode Node, int Depth, string? Kind, bool Cyclic) : DependencyGraphItem;

    private sealed record DependencyGraphWarningItem(int Depth, string Message) : DependencyGraphItem;
}
