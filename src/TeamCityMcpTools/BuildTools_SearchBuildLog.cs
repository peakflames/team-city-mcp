namespace TeamCityMcpTools;

public partial class BuildTools
{
    private const int LogSearchMaxOutputBytes = 40_000;
    private const long LogSearchMaxScanBytes = 100_000_000;

    [McpServerTool(Name = TeamCityToolNames.SearchBuildLog),
        Description(
            "Searches a build's full console log (downloaded server-side) for a regex or literal pattern and " +
            "returns matched lines with surrounding context, merging overlapping windows. Use this to find " +
            "assertion failures, stack traces, or other output around a build failure without downloading the " +
            "(often multi-MB) log yourself.")]
    public async Task<string> SearchBuildLog(
        [Description("The TeamCity build ID (numeric).")]
        string buildId,

        [Description("Regex pattern to search for (falls back to a literal substring match if not valid regex).")]
        string pattern,

        [Description("Number of lines of context to include before and after each match. Defaults to 4.")]
        int contextLines = 4,

        [Description("Maximum number of matches to return. Defaults to 20.")]
        int maxMatches = 20,

        [Description("Case-insensitive search. Defaults to true.")]
        bool ignoreCase = true)
    {
        if (contextLines < 0)
            return "ERROR: contextLines must be >= 0.";
        if (maxMatches < 1)
            return "ERROR: maxMatches must be >= 1.";

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        var usedLiteralFallback = false;
        Regex regex;
        try
        {
            regex = new Regex(pattern, MakeRegexOptions(ignoreCase));
        }
        catch (ArgumentException)
        {
            usedLiteralFallback = true;
            regex = new Regex(Regex.Escape(pattern), MakeRegexOptions(ignoreCase));
        }

        try
        {
            var response = await GetBuildLogResponseAsync(client, buildId);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build log for build '{buildId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var totalBytes = response.Content.Headers.ContentLength;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var (windows, totalMatches, matchCapHit, scanCapHit) =
                await SearchLogStreamAsync(stream, regex, contextLines, maxMatches, LogSearchMaxScanBytes);

            var sb = new StringBuilder();
            sb.AppendLine("# Build Log Search");
            sb.AppendLine();
            sb.AppendLine($"**Build ID:** {buildId}");
            sb.AppendLine($"**Pattern:** `{pattern}`{(usedLiteralFallback ? " *(invalid regex — matched as literal text)*" : string.Empty)}");
            sb.AppendLine($"**Case-insensitive:** {ignoreCase}");
            if (totalBytes.HasValue)
                sb.AppendLine($"**Log size:** {totalBytes.Value:N0} bytes");
            sb.AppendLine($"**Matches found:** {totalMatches}{(matchCapHit ? $" (stopped after {maxMatches} matches — narrow the pattern for a complete count)" : string.Empty)}");
            sb.AppendLine();

            if (windows.Count == 0)
            {
                sb.AppendLine("No matches found.");
                return sb.ToString();
            }

            AppendLogWindows(sb, windows, LogSearchMaxOutputBytes);

            if (scanCapHit)
                sb.AppendLine($"\n**Note:** Stopped scanning after {LogSearchMaxScanBytes:N0} bytes without reaching the end of the log.");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to search build log — {ex.Message}";
        }
    }

    private static RegexOptions MakeRegexOptions(bool ignoreCase) =>
        RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

    private static Task<HttpResponseMessage> GetBuildLogResponseAsync(TeamCityClient client, string buildId) =>
        client.HttpClient.GetAsync(
            $"downloadBuildLog.html?buildId={Uri.EscapeDataString(buildId)}",
            HttpCompletionOption.ResponseHeadersRead);

    private sealed class LogWindow
    {
        public required List<(int LineNumber, string Text)> Lines { get; init; }
    }

    /// <summary>
    /// Incrementally groups matched lines (plus surrounding context) into merged windows as lines are fed in
    /// one at a time, so it can drive either a streaming reader or an in-memory line list.
    /// </summary>
    private sealed class LogWindowMatcher
    {
        private readonly Regex _regex;
        private readonly int _contextLines;
        private readonly int _maxMatches;
        private readonly Queue<(int LineNumber, string Text)> _ring;
        private List<(int LineNumber, string Text)>? _current;
        private int _windowCloseAt = -1;

        public List<LogWindow> Windows { get; } = new();
        public int TotalMatches { get; private set; }
        public bool MatchCapHit { get; private set; }

        public LogWindowMatcher(Regex regex, int contextLines, int maxMatches)
        {
            _regex = regex;
            _contextLines = contextLines;
            _maxMatches = maxMatches;
            _ring = new Queue<(int, string)>(Math.Max(contextLines, 1));
        }

        /// <returns>True if the caller can stop feeding lines (match cap hit and no window still open).</returns>
        public bool ProcessLine(int lineNumber, string line)
        {
            var isMatch = !MatchCapHit && _regex.IsMatch(line);

            if (isMatch)
            {
                TotalMatches++;
                _current ??= new List<(int, string)>(_ring);
                _current.Add((lineNumber, line));
                _windowCloseAt = lineNumber + _contextLines;
                if (TotalMatches >= _maxMatches)
                    MatchCapHit = true;
            }
            else if (_current != null)
            {
                if (lineNumber <= _windowCloseAt)
                {
                    _current.Add((lineNumber, line));
                }
                else
                {
                    Windows.Add(new LogWindow { Lines = _current });
                    _current = null;
                }
            }

            _ring.Enqueue((lineNumber, line));
            if (_ring.Count > _contextLines)
                _ring.Dequeue();

            return MatchCapHit && _current == null;
        }

        public void Finish()
        {
            if (_current == null)
                return;

            Windows.Add(new LogWindow { Lines = _current });
            _current = null;
        }
    }

    private static async Task<(List<LogWindow> Windows, int TotalMatches, bool MatchCapHit, bool ScanCapHit)> SearchLogStreamAsync(
        Stream stream, Regex regex, int contextLines, int maxMatches, long maxScanBytes)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var matcher = new LogWindowMatcher(regex, contextLines, maxMatches);
        var lineNumber = 0;
        long scannedBytes = 0;
        var scanCapHit = false;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            scannedBytes += Encoding.UTF8.GetByteCount(line) + 1;
            if (scannedBytes > maxScanBytes)
            {
                scanCapHit = true;
                break;
            }

            if (matcher.ProcessLine(lineNumber, line))
                break;
        }

        matcher.Finish();
        return (matcher.Windows, matcher.TotalMatches, matcher.MatchCapHit, scanCapHit);
    }

    private static async Task<(List<string> Lines, bool ScanCapHit)> ReadLogLinesAsync(
        HttpResponseMessage response, long maxScanBytes)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        long scannedBytes = 0;
        var scanCapHit = false;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            scannedBytes += Encoding.UTF8.GetByteCount(line) + 1;
            if (scannedBytes > maxScanBytes)
            {
                scanCapHit = true;
                break;
            }

            lines.Add(line);
        }

        return (lines, scanCapHit);
    }

    /// <summary>Appends formatted windows to <paramref name="sb"/> until <paramref name="budgetBytes"/> is exhausted.</summary>
    /// <returns>The remaining byte budget after appending.</returns>
    private static int AppendLogWindows(StringBuilder sb, List<LogWindow> windows, int budgetBytes)
    {
        var truncated = false;
        var shown = 0;

        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            var first = window.Lines[0].LineNumber;
            var last = window.Lines[^1].LineNumber;

            var block = new StringBuilder();
            if (shown > 0)
                block.AppendLine("…");
            block.AppendLine($"**Lines {first}-{last}:**");
            block.AppendLine("```");
            foreach (var (lineNumber, text) in window.Lines)
                block.AppendLine($"{lineNumber,6}: {text}");
            block.AppendLine("```");

            if (block.Length > budgetBytes)
            {
                truncated = true;
                break;
            }

            sb.Append(block);
            budgetBytes -= block.Length;
            shown++;
        }

        if (truncated)
        {
            sb.AppendLine();
            sb.AppendLine($"**Note:** Output truncated at ~{LogSearchMaxOutputBytes:N0} bytes — {shown} of {windows.Count} " +
                           "window(s) shown. Narrow the pattern or reduce contextLines/maxMatches for full coverage.");
        }

        return budgetBytes;
    }
}
