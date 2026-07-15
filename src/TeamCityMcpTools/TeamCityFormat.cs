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
}
