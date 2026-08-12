namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = TeamCityToolNames.ListBuildArtifacts),
        Description(
            "Lists artifact files and directories produced by a build, including hidden " +
            "'.teamcity/...' entries (e.g. build settings digests). Optionally navigate into a " +
            "subdirectory via 'path'.")]
    public async Task<string> ListBuildArtifacts(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Optional artifact subdirectory path to list (e.g. 'reports/coverage'). Defaults to the artifact root.")]
        string? path = null)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var segments = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Any(s => s == ".."))
                    return $"ERROR: Invalid path '{path}' — path traversal is not allowed.";
            }

            var pathSegment = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : $"/{string.Join('/', path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString))}";
            var fields = "count,file(name,size,modificationTime,children)";
            var url = $"app/rest/builds/id:{Uri.EscapeDataString(buildId)}/artifacts/children{pathSegment}?locator=hidden:any&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build '{buildId}' or path '{path}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var artifacts = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.ArtifactChildrenResponse);

            if (artifacts is null)
                return $"ERROR: Unable to parse artifact listing for build '{buildId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Artifacts");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Path:** {(string.IsNullOrWhiteSpace(path) ? "/ (root)" : path)}");
            sb.AppendLine();

            if (artifacts.File is not { Count: > 0 } files)
            {
                sb.AppendLine("No artifacts found at this path.");
                return sb.ToString();
            }

            sb.AppendLine("| Type | Name | Size | Modified |");
            sb.AppendLine("|------|------|------|----------|");

            foreach (var file in files)
            {
                var isDir = file.Children is not null;
                var type = isDir ? "dir" : "file";
                var size = file.Size.HasValue ? file.Size.Value.ToString() : "—";
                sb.AppendLine($"| {type} | {file.Name} | {size} | {TeamCityFormat.FormatTcDate(file.ModificationTime)} |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list build artifacts — {ex.Message}";
        }
    }
}
