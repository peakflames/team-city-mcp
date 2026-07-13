namespace TeamCityMcpTools;

internal static class TeamCityFormat
{
    // TeamCity date format: 20241119T102304+0000
    internal static string FormatTcDate(string? tcDate)
    {
        if (string.IsNullOrWhiteSpace(tcDate) || tcDate.Length < 15)
            return tcDate ?? "—";
        return $"{tcDate[..4]}-{tcDate[4..6]}-{tcDate[6..8]} {tcDate[9..11]}:{tcDate[11..13]}";
    }
}
