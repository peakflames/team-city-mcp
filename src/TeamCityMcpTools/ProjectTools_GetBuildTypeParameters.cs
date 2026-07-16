namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_build_type_parameters"),
        Description(
            "Gets the configuration parameters defined on a TeamCity build configuration (not a specific build) — " +
            "the same values shown in the TeamCity 'Parameters' admin page. Each parameter shows whether it is " +
            "defined directly on this build configuration or inherited from a template, plus its control type and " +
            "label from the configuration spec. Use this instead of resolving a specific build ID to inspect what " +
            "is actually configured. Use 'nameFilter' to search by substring — configurations can have 100+ params.")]
    public async Task<string> GetBuildTypeParameters(
        [Description("The TeamCity build type ID (e.g., 'MyProject_Build').")]
        string buildTypeId,

        [Description("Optional case-insensitive substring to filter parameter names by (e.g. 'branch', 'timeout').")]
        string? nameFilter = null,

        [Description("Which parameters to include: 'all' (default), 'own' (defined directly on this build type), " +
                      "or 'inherited' (defined on a template).")]
        string inheritance = "all",

        [Description("Maximum number of matching parameters to display. Defaults to 100.")]
        int count = 100)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "count,property(name,value,inherited,type(rawValue))";
            var url = $"app/rest/buildTypes/id:{buildTypeId}/parameters?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build type with ID '{buildTypeId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeParameterListResponse);

            if (parameters is null)
                return $"ERROR: Unable to parse build type parameters for '{buildTypeId}'.";

            var sb = new StringBuilder();
            sb.AppendLine("# Build Type Parameters");
            sb.AppendLine();
            sb.AppendLine($"**Build Type ID:** {buildTypeId}");
            sb.AppendLine($"**Total Parameters:** {parameters.Count ?? 0}");
            if (!string.IsNullOrWhiteSpace(nameFilter))
                sb.AppendLine($"**Name Filter:** {nameFilter}");
            if (!string.Equals(inheritance, "all", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"**Inheritance Filter:** {inheritance}");
            sb.AppendLine();

            if (parameters.Property is not { Count: > 0 } allProperties)
            {
                sb.AppendLine("No parameters found for this build type.");
                return sb.ToString();
            }

            IEnumerable<BuildTypeParameterEntry> filtered = allProperties;

            if (!string.IsNullOrWhiteSpace(nameFilter))
                filtered = filtered.Where(p => p.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) == true);

            filtered = inheritance.ToLowerInvariant() switch
            {
                "own" => filtered.Where(p => p.Inherited != true),
                "inherited" => filtered.Where(p => p.Inherited == true),
                _ => filtered,
            };

            var ordered = filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var displayed = ordered.Take(count).ToList();

            if (displayed.Count == 0)
            {
                sb.AppendLine("No parameters matched the given filters.");
                return sb.ToString();
            }

            sb.AppendLine($"**Showing:** {displayed.Count} of {ordered.Count} matching parameters");
            sb.AppendLine();
            sb.AppendLine("| Name | Value | Source | Control | Label |");
            sb.AppendLine("|------|-------|--------|---------|-------|");

            foreach (var property in displayed)
            {
                var source = property.Inherited == true ? "inherited" : "own";
                var (controlType, label, display) = ParseTypeSpec(property.Type?.RawValue);
                var controlDisplay = string.Equals(display, "hidden", StringComparison.OrdinalIgnoreCase)
                    ? $"{controlType} *(hidden)*"
                    : controlType;
                sb.AppendLine($"| {property.Name} | {property.Value} | {source} | {controlDisplay} | {label ?? "—"} |");
            }

            if (ordered.Count > displayed.Count)
                sb.AppendLine($"\n*{ordered.Count - displayed.Count} more parameters not shown — narrow with 'nameFilter' or raise 'count'.*");

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build type parameters — {ex.Message}";
        }
    }

    /// <summary>
    /// Parses a TeamCity parameter "type" spec string (e.g.
    /// "text description='...' label='Marker Filters' validationMode='any' display='normal'") into its
    /// leading control type token plus the 'label' and 'display' key values. TeamCity escapes literal single
    /// quotes inside values as "|'" and newlines as "|n" — an unescaped "'" (not preceded by "|") ends the value.
    /// </summary>
    private static (string ControlType, string? Label, string? Display) ParseTypeSpec(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return ("—", null, null);

        var spaceIdx = rawValue.IndexOf(' ');
        var controlType = spaceIdx < 0 ? rawValue : rawValue[..spaceIdx];

        string? ExtractKey(string key)
        {
            var match = Regex.Match(rawValue, $@"\b{key}='(?<val>.*?)(?<!\|)'", RegexOptions.Singleline);
            if (!match.Success)
                return null;
            return match.Groups["val"].Value.Replace("|'", "'").Replace("|n", " ").Trim();
        }

        return (controlType, ExtractKey("label"), ExtractKey("display"));
    }
}
