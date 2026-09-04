namespace TeamCityMcpTools;

/// <summary>
/// Builds and escapes TeamCity REST API locator dimensions. TeamCity URL-decodes a locator string before
/// parsing its dimensions, so a caller-supplied value that reaches a locator via naive string interpolation
/// can inject ',' ':' '(' ')' to add or override dimensions the tool never intended to send (e.g. escaping
/// out of a "branch:" filter to add an unrelated "affectedProject:" filter). Every value that flows into a
/// locator must go through <see cref="EscapeValue"/> or <see cref="Dimension"/>.
/// </summary>
internal static class TeamCityLocator
{
    private static readonly Regex SafeIdPattern = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
    private static readonly Regex NumericIdPattern = new("^[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex SafeValuePattern = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

    /// <summary>True when <paramref name="value"/> matches TeamCity's own id charset — safe to interpolate as-is.</summary>
    internal static bool IsSafeId(string? value) =>
        !string.IsNullOrEmpty(value) && SafeIdPattern.IsMatch(value);

    /// <summary>True when <paramref name="value"/> is a non-empty run of digits — the shape of a TeamCity build id.</summary>
    internal static bool IsNumericId(string? value) =>
        !string.IsNullOrEmpty(value) && NumericIdPattern.IsMatch(value);

    /// <summary>
    /// Returns <paramref name="value"/> verbatim when it matches a conservative safe charset. Otherwise wraps it
    /// as a "$base64:" literal (TeamCity's own escape hatch for locator dimension values), so any character with
    /// meaning to the locator parser — ',' ':' '(' ')' — is neutralized instead of interpreted.
    /// </summary>
    internal static string EscapeValue(string value)
    {
        if (SafeValuePattern.IsMatch(value))
            return value;

        var bytes = Encoding.UTF8.GetBytes(value);
        var base64Url = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"$base64:{base64Url}";
    }

    /// <summary>Builds a single "name:(value)" locator dimension with the value safely escaped.</summary>
    internal static string Dimension(string name, string value) => $"{name}:({EscapeValue(value)})";
}
