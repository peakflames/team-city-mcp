namespace TeamCityMcpTools;

internal static class TeamCityFormat
{
    /// <summary>Safe ceiling for a single tool's markdown output, kept well under typical MCP client token caps.</summary>
    internal const int MaxToolOutputChars = 45_000;

    // TeamCity date format: 20241119T102304+0000
    internal static string FormatTcDate(string? tcDate)
    {
        if (string.IsNullOrWhiteSpace(tcDate) || tcDate.Length < 15)
            return tcDate ?? "—";
        return $"{tcDate[..4]}-{tcDate[4..6]}-{tcDate[6..8]} {tcDate[9..11]}:{tcDate[11..13]}";
    }

    /// <summary>
    /// Truncates long text by keeping a head and tail slice and noting how much was omitted in between.
    /// Generic — makes no assumption about the text's source (test runner, log, artifact, etc.).
    /// </summary>
    internal static string Truncate(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text ?? string.Empty;

        var headLen = maxChars * 3 / 4;
        var tailLen = maxChars - headLen;
        var omitted = text.Length - headLen - tailLen;

        return $"{text[..headLen]}\n… [{omitted:N0} chars omitted — narrow the query for full text] …\n{text[^tailLen..]}";
    }

    /// <summary>
    /// Normalizes text into a grouping key so byte-for-byte-equivalent content (e.g. two tests that failed with
    /// identical captured output) can be deduplicated. Deliberately conservative — whitespace normalization only,
    /// no language- or tool-specific parsing — so genuinely different failures are never merged together.
    /// </summary>
    internal static string Fingerprint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r\n", "\n").Trim();
        return Regex.Replace(normalized, @"[ \t]+", " ");
    }

    /// <summary>
    /// Returns a short, generic head-only preview of <paramref name="text"/> — the first <paramref name="maxChars"/>
    /// characters, with a note if more follows. Deliberately a plain length cut, not a single line: most test/build
    /// tools (pytest, JUnit, MATLAB test frameworks, etc.) put the failing call chain and error summary before any
    /// captured stdout/log dump, so a short head slice usually surfaces the useful part without parsing any
    /// framework-specific marker (no "E " prefix, no "Caused by", nothing tool-specific).
    /// </summary>
    internal static string Preview(string? text, int maxChars = 800)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var trimmed = text.Trim();
        return trimmed.Length > maxChars
            ? $"{trimmed[..maxChars]}…\n*(truncated preview — use detailsMode:\"full\" for more)*"
            : trimmed;
    }

    /// <summary>
    /// Safety-net cap for a tool's final markdown output. If <paramref name="markdown"/> exceeds
    /// <paramref name="maxChars"/>, truncates at the last newline before the cap and appends a note — so
    /// output is never silently cut off without saying so.
    /// </summary>
    internal static string Clamp(string markdown, int maxChars = MaxToolOutputChars)
    {
        if (markdown.Length <= maxChars)
            return markdown;

        var cut = markdown.LastIndexOf('\n', maxChars - 1);
        if (cut <= 0)
            cut = maxChars;

        return $"{markdown[..cut]}\n\n**Note:** Output truncated at ~{maxChars:N0} characters — " +
               "narrow the query (filters, count, contextLines, maxMatches, etc.) for full coverage.";
    }

    /// <summary>
    /// Compiles an optional case-insensitive regex filter pattern. Returns Ok(null) when
    /// <paramref name="pattern"/> is null/blank (no filter), Ok(regex) when valid, or a Fail result
    /// carrying the exception message when the pattern is invalid or pathological.
    /// </summary>
    internal static Result<Regex?> CompileFilter(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return Result.Ok<Regex?>(null);
        try
        {
            return Result.Ok<Regex?>(new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)));
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Formats a millisecond duration as both a humanized h/m/s string and the raw millisecond count, e.g.
    /// "5m 0.5s (300,491 ms)" or "48.4s (48,383 ms)". Sub-second durations render as plain "N ms" since a
    /// humanized form would add nothing. Both forms are always present so clients that want ms precision
    /// never lose it to the humanized display.
    /// </summary>
    internal static string FormatDurationMs(long ms)
    {
        if (ms < 1000)
            return $"{ms:N0} ms";

        var totalSeconds = ms / 1000.0;
        var hours = (int)(totalSeconds / 3600);
        var minutes = (int)(totalSeconds % 3600 / 60);
        var seconds = totalSeconds % 60;

        var parts = new List<string>();
        if (hours > 0)
            parts.Add($"{hours}h");
        if (hours > 0 || minutes > 0)
            parts.Add($"{minutes}m");
        parts.Add(hours > 0 ? $"{(int)seconds}s" : $"{seconds:0.#}s");

        return $"{string.Join(" ", parts)} ({ms:N0} ms)";
    }

    /// <summary>Same as <see cref="FormatDurationMs"/> but for a duration already expressed in whole seconds.</summary>
    internal static string FormatDurationSeconds(long seconds) => FormatDurationMs(seconds * 1000);

    /// <summary>
    /// Sanitizes an arbitrary TeamCity ID into a syntactically valid Mermaid node identifier — Mermaid node IDs
    /// cannot safely contain most punctuation, so anything outside [A-Za-z0-9_] is replaced with '_'.
    /// </summary>
    internal static string SanitizeMermaidId(string prefix, string rawId)
    {
        var cleaned = Regex.Replace(rawId, "[^A-Za-z0-9_]", "_");
        return $"{prefix}_{cleaned}";
    }

    /// <summary>
    /// Escapes text for use inside a quoted Mermaid node/edge label. Mermaid labels are quoted with double
    /// quotes, so any embedded quote is replaced with its HTML entity and newlines are flattened — otherwise
    /// labels like build type names containing "[Test Group 1]" or embedded quotes break the diagram syntax.
    /// </summary>
    internal static string EscapeMermaidLabel(string label) =>
        label.Replace("\"", "#quot;").Replace("\r\n", " ").Replace("\n", " ");
}
