namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetBuildTypeDependencyGraph),
        Description(
            "Renders the design-time dependency configuration graph for a build type — both snapshot " +
            "and artifact dependencies, distinct from any actual build run. Shows forward Dependencies " +
            "(what this build type is configured to depend on, labeled [snapshot]/[artifact]) and/or " +
            "reverse Dependents (what depends on it — snapshot dependents only, since TeamCity has no " +
            "reverse locator for artifact dependencies). Format 'mermaid' renders a single combined " +
            "```mermaid graph LR``` block — matching the TeamCity build-chain UI, dependencies (upstream) " +
            "on the left, this build type in the middle, dependents (downstream) on the right, arrows " +
            "always flowing dependency -> dependent.")]
    public async Task<string> GetBuildTypeDependencyGraph(
        [Description("The TeamCity build type ID (e.g., 'MyProject_Build').")]
        string buildTypeId,

        [Description("Which relationships to render: 'dependencies', 'dependents', or 'both' (default).")]
        string only = "both",

        [Description("Maximum depth to walk. Defaults to 10.")]
        int depth = 10,

        [Description("Output format: 'markdown' (default) or 'mermaid' (fenced ```mermaid graph LR``` block).")]
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
        var showDependencies = !string.Equals(only, "dependents", StringComparison.OrdinalIgnoreCase);
        var showDependents = !string.Equals(only, "dependencies", StringComparison.OrdinalIgnoreCase);
        var mermaid = string.Equals(format, "mermaid", StringComparison.OrdinalIgnoreCase);

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

            if (root is null || root.Id is null)
                return $"ERROR: Unable to parse build type details for '{buildTypeId}'.";

            var rootNode = new DependencyGraphNode(root.Id, root.Name, root.ProjectName);

            var dependencyItems = new List<DependencyGraphItem>();
            if (showDependencies)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };
                await CollectForwardDependencies(client, dependencyItems, root.Id, 1, depth, visited);
            }

            var dependentItems = new List<DependencyGraphItem>();
            if (showDependents)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };
                await CollectReverseDependents(client, dependentItems, root.Id, 1, depth, visited);
            }

            return mermaid
                ? RenderBuildTypeDependencyMermaid(rootNode, dependencyItems, dependentItems, showDependencies, showDependents, depth)
                : RenderBuildTypeDependencyMarkdown(rootNode, dependencyItems, dependentItems, showDependencies, showDependents, depth);
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build type dependency graph — {ex.Message}";
        }
    }

    private static string RenderBuildTypeDependencyMarkdown(
        DependencyGraphNode root,
        List<DependencyGraphItem> dependencyItems,
        List<DependencyGraphItem> dependentItems,
        bool showDependencies,
        bool showDependents,
        int depth)
    {
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
            AppendBuildTypeItems(sb, dependencyItems);
            sb.AppendLine();
        }

        if (showDependents)
        {
            sb.AppendLine("## Dependents (what depends on this)");
            sb.AppendLine();
            sb.AppendLine("*Note: only snapshot dependents are shown — TeamCity has no reverse locator for artifact dependencies.*");
            sb.AppendLine();
            sb.AppendLine($"- **{root.Name}** (`{root.Id}`)");
            AppendBuildTypeItems(sb, dependentItems);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendBuildTypeItems(StringBuilder sb, List<DependencyGraphItem> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case DependencyGraphNodeItem node:
                {
                    var indent = new string(' ', node.Depth * 2);
                    var note = node.Cyclic ? " *(cycle — already visited)*" : string.Empty;
                    var projectSuffix = string.IsNullOrWhiteSpace(node.Node.ProjectName) ? string.Empty : $" — {node.Node.ProjectName}";
                    var kindLabel = node.Kind is null ? string.Empty : $" [{node.Kind}{node.RevisionLabel}]";
                    sb.AppendLine($"{indent}- **{node.Node.Name}** (`{node.Node.Id}`){projectSuffix}{kindLabel}{note}");
                    break;
                }
                case DependencyGraphWarningItem warning:
                    sb.AppendLine($"{new string(' ', warning.Depth * 2)}- ⚠️ {warning.Message}");
                    break;
            }
        }
    }

    private static string RenderBuildTypeDependencyMermaid(
        DependencyGraphNode root,
        List<DependencyGraphItem> dependencyItems,
        List<DependencyGraphItem> dependentItems,
        bool showDependencies,
        bool showDependents,
        int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Build Type Dependency Graph");
        sb.AppendLine();
        sb.AppendLine($"**Build Type:** {root.ProjectName} / {root.Name} (`{root.Id}`)");
        sb.AppendLine($"**Max Depth:** {depth}");
        sb.AppendLine();
        sb.AppendLine("*Orientation: dependencies (upstream) on the left, dependents (downstream) on the right — " +
                       "arrows flow dependency -> dependent.*");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");

        var declared = new HashSet<string>(StringComparer.Ordinal);

        void DeclareNode(DependencyGraphNode node, bool isRoot)
        {
            if (!declared.Add(node.Id))
                return;
            var id = TeamCityFormat.SanitizeMermaidId("t", node.Id);
            var label = TeamCityFormat.EscapeMermaidLabel($"{node.Name} ({node.Id})");
            sb.AppendLine(isRoot ? $"    {id}[\"{label}\"]:::root" : $"    {id}[\"{label}\"]");
        }

        DeclareNode(root, true);
        foreach (var item in dependencyItems)
        {
            if (item is DependencyGraphNodeItem node)
                DeclareNode(node.Node, false);
        }
        foreach (var item in dependentItems)
        {
            if (item is DependencyGraphNodeItem node)
                DeclareNode(node.Node, false);
        }

        sb.AppendLine();

        var emittedEdges = new HashSet<(string Source, string Target)>();

        if (showDependencies)
        {
            foreach (var item in dependencyItems)
            {
                if (item is not DependencyGraphNodeItem node)
                    continue;

                // Node was found as a dependency of FromId, so arrow flows dependency (Node) -> dependent (FromId).
                EmitEdge(sb, emittedEdges, node.Node.Id, node.FromId, node.Kind);
            }
        }

        if (showDependents)
        {
            foreach (var item in dependentItems)
            {
                if (item is not DependencyGraphNodeItem node)
                    continue;

                // Node was found as a dependent of FromId, so arrow flows dependency (FromId) -> dependent (Node).
                EmitEdge(sb, emittedEdges, node.FromId, node.Node.Id, node.Kind ?? "snapshot");
            }
        }

        sb.AppendLine();
        sb.AppendLine("    classDef root fill:#f9c74f,stroke:#333,stroke-width:2px;");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static void EmitEdge(StringBuilder sb, HashSet<(string Source, string Target)> emittedEdges, string sourceBuildTypeId, string targetBuildTypeId, string? kind)
    {
        if (!emittedEdges.Add((sourceBuildTypeId, targetBuildTypeId)))
            return;

        var sourceId = TeamCityFormat.SanitizeMermaidId("t", sourceBuildTypeId);
        var targetId = TeamCityFormat.SanitizeMermaidId("t", targetBuildTypeId);
        var label = kind is null ? string.Empty : $"|{kind}|";
        sb.AppendLine($"    {sourceId} -->{label} {targetId}");
    }

    private static async Task CollectForwardDependencies(
        TeamCityClient client,
        List<DependencyGraphItem> items,
        string buildTypeId,
        int currentDepth,
        int maxDepth,
        HashSet<string> visited)
    {
        if (currentDepth > maxDepth)
            return;

        var snapshotFields = "count,snapshot-dependency(id,source-buildType(id,name,projectName))";
        var snapshotUrl = $"app/rest/buildTypes/id:{Uri.EscapeDataString(buildTypeId)}/snapshot-dependencies?fields={Uri.EscapeDataString(snapshotFields)}";

        var snapshotResponse = await client.HttpClient.GetAsync(snapshotUrl);
        if (!snapshotResponse.IsSuccessStatusCode)
        {
            items.Add(new DependencyGraphWarningItem(currentDepth,
                $"Failed to load dependencies ({(int)snapshotResponse.StatusCode}: {snapshotResponse.ReasonPhrase})"));
            return;
        }

        var snapshotJson = await snapshotResponse.Content.ReadAsStringAsync();
        var snapshotList = JsonSerializer.Deserialize(snapshotJson, TeamCityJsonContext.Default.SnapshotDependenciesWrapper);

        var artifactFields = "count,artifact-dependency(id,disabled,source-buildType(id,name,projectName),properties(property(name,value)))";
        var artifactUrl = $"app/rest/buildTypes/id:{Uri.EscapeDataString(buildTypeId)}/artifact-dependencies?fields={Uri.EscapeDataString(artifactFields)}";

        var artifactResponse = await client.HttpClient.GetAsync(artifactUrl);
        if (!artifactResponse.IsSuccessStatusCode)
        {
            items.Add(new DependencyGraphWarningItem(currentDepth,
                $"Failed to load artifact dependencies ({(int)artifactResponse.StatusCode}: {artifactResponse.ReasonPhrase})"));
            return;
        }

        var artifactJson = await artifactResponse.Content.ReadAsStringAsync();
        var artifactList = JsonSerializer.Deserialize(artifactJson, TeamCityJsonContext.Default.ArtifactDependenciesWrapper);

        var merged = new Dictionary<string, (DependencyBuildTypeRef Source, bool Snapshot, bool Artifact, string? Revision)>(StringComparer.Ordinal);

        foreach (var dependency in snapshotList?.SnapshotDependency ?? [])
        {
            var source = dependency.SourceBuildType;
            if (source?.Id is null)
                continue;

            merged[source.Id] = merged.TryGetValue(source.Id, out var existing)
                ? (source, true, existing.Artifact, existing.Revision)
                : (source, true, false, null);
        }

        foreach (var dependency in artifactList?.ArtifactDependency ?? [])
        {
            var source = dependency.SourceBuildType;
            if (source?.Id is null)
                continue;

            var revision = dependency.Properties?.Property?.FirstOrDefault(p => p.Name == "revisionName")?.Value;

            merged[source.Id] = merged.TryGetValue(source.Id, out var existing)
                ? (source, existing.Snapshot, true, revision)
                : (source, false, true, revision);
        }

        if (merged.Count == 0)
            return;

        foreach (var (source, isSnapshot, isArtifact, revision) in merged.Values)
        {
            var cyclic = visited.Contains(source.Id!);
            var kind = isSnapshot && isArtifact ? "snapshot+artifact" : isSnapshot ? "snapshot" : "artifact";
            var revisionLabel = isArtifact && !string.IsNullOrWhiteSpace(revision) ? $": {revision}" : string.Empty;
            var graphNode = new DependencyGraphNode(source.Id!, source.Name, source.ProjectName);
            items.Add(new DependencyGraphNodeItem(buildTypeId, graphNode, currentDepth, kind, revisionLabel, cyclic));

            if (cyclic)
                continue;

            visited.Add(source.Id!);
            await CollectForwardDependencies(client, items, source.Id!, currentDepth + 1, maxDepth, visited);
        }
    }

    private static async Task CollectReverseDependents(
        TeamCityClient client,
        List<DependencyGraphItem> items,
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
            items.Add(new DependencyGraphWarningItem(currentDepth,
                $"Failed to load dependents ({(int)response.StatusCode}: {response.ReasonPhrase})"));
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

            var cyclic = visited.Contains(dependent.Id);
            var graphNode = new DependencyGraphNode(dependent.Id, dependent.Name, dependent.ProjectName);
            items.Add(new DependencyGraphNodeItem(buildTypeId, graphNode, currentDepth, null, string.Empty, cyclic));

            if (cyclic)
                continue;

            visited.Add(dependent.Id);
            await CollectReverseDependents(client, items, dependent.Id, currentDepth + 1, maxDepth, visited);
        }
    }

    private sealed record DependencyGraphNode(string Id, string? Name, string? ProjectName);

    private abstract record DependencyGraphItem;

    private sealed record DependencyGraphNodeItem(string FromId, DependencyGraphNode Node, int Depth, string? Kind, string? RevisionLabel, bool Cyclic) : DependencyGraphItem;

    private sealed record DependencyGraphWarningItem(int Depth, string Message) : DependencyGraphItem;
}
