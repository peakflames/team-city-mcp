namespace TeamCityMcpTools;

public partial class BuildTools
{
    private const int ArtifactContentMaxBytes = 200_000;
    private const int ArtifactContentMaxLines = 500;

    [McpServerTool(Name = "teamcity_get_build_artifact_content"),
        Description(
            "Gets the text content of a single build artifact file (e.g. a log, report, or settings " +
            "digest). Refuses binary files and truncates large text files to the first "
            + "200 KB / 500 lines, whichever is reached first.")]
    public async Task<string> GetBuildArtifactContent(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("The artifact path relative to the artifact root (e.g. '.teamcity/settings-digest.txt' or 'reports/summary.log').")]
        string path)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var url = $"app/rest/builds/id:{buildId}/artifacts/content/{path.TrimStart('/')}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Artifact '{path}' was not found for build '{buildId}'.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var bytes = await response.Content.ReadAsByteArrayAsync();

            var isDeclaredText = contentType.Length == 0
                                  || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                                  || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                                  || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                                  || contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase);
            var sniffLength = Math.Min(bytes.Length, 8000);
            var containsNullByte = Array.IndexOf(bytes, (byte)0, 0, sniffLength) >= 0;

            if (containsNullByte || !isDeclaredText)
                return $"ERROR: Artifact '{path}' appears to be binary (content-type '{contentType}') — refusing to dump binary content. Use teamcity_list_build_artifacts to inspect its size/metadata instead.";

            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var truncatedForSize = bytes.Length > ArtifactContentMaxBytes;

            var lines = text.Split('\n');
            var truncatedForLines = lines.Length > ArtifactContentMaxLines;

            string displayText;
            if (truncatedForSize)
            {
                var byteSlice = bytes[..ArtifactContentMaxBytes];
                displayText = System.Text.Encoding.UTF8.GetString(byteSlice);
            }
            else if (truncatedForLines)
            {
                displayText = string.Join('\n', lines.Take(ArtifactContentMaxLines));
            }
            else
            {
                displayText = text;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Build Artifact Content");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Path:** {path}");
            sb.AppendLine($"**Size:** {bytes.Length} bytes");
            if (truncatedForSize)
                sb.AppendLine($"**Note:** Truncated to the first {ArtifactContentMaxBytes} bytes (full artifact is larger).");
            else if (truncatedForLines)
                sb.AppendLine($"**Note:** Truncated to the first {ArtifactContentMaxLines} lines (full artifact has {lines.Length} lines).");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(displayText);
            sb.AppendLine("```");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build artifact content — {ex.Message}";
        }
    }
}
