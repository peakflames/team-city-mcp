namespace TeamCityMcpTools;

public partial class BuildTools
{
    [McpServerTool(Name = "teamcity_list_mutes"),
        Description(
            "Gets TeamCity mute details from /app/rest/mutes — the reason/comment, who muted it and when, the " +
            "scope (project or build configuration(s)) it applies to, and its resolution policy ('manually', " +
            "'when fixed', or 'at' a specific time). Complements 'teamcity_get_build_tests', which shows *whether* " +
            "a test occurrence is muted (tagged '(muted)') but not why. Provide 'buildId' to resolve to the build " +
            "configuration a specific build belongs to, 'buildTypeId' to scope to one build configuration, or " +
            "'projectId' to scope to a project (including its subprojects); omit all three for a server-wide " +
            "list. 'testNameFilter' narrows to mutes whose target test name contains the given substring. If both " +
            "'buildId' and 'buildTypeId' are given, the build configuration resolved from 'buildId' takes " +
            "precedence.")]
    public async Task<string> ListMutes(
        [Description("Optional TeamCity build ID (numeric). Resolves to the build's build configuration and scopes the mute lookup to it.")]
        string? buildId = null,

        [Description("Optional TeamCity build type (build configuration) ID to scope the mute lookup to.")]
        string? buildTypeId = null,

        [Description("Optional TeamCity project ID to scope the mute lookup to (includes subprojects).")]
        string? projectId = null,

        [Description("Optional case-insensitive substring filter against muted test names.")]
        string? testNameFilter = null,

        [Description("Maximum number of mutes to fetch from the server before filtering. Defaults to 50.")]
        int count = 50)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var resolvedBuildTypeId = buildTypeId;
            var resolvedProjectId = projectId;

            if (!string.IsNullOrWhiteSpace(buildId))
            {
                var buildFields = "buildTypeId,buildType(id,projectId)";
                var buildUrl = $"app/rest/builds/id:{buildId}?fields={Uri.EscapeDataString(buildFields)}";
                var buildResponse = await client.HttpClient.GetAsync(buildUrl);

                if (buildResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return "ERROR: Authentication failed — check the access token.";
                if (buildResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return $"ERROR: Build with ID '{buildId}' was not found.";
                if (!buildResponse.IsSuccessStatusCode)
                    return $"ERROR: TeamCity returned {(int)buildResponse.StatusCode}: {buildResponse.ReasonPhrase}";

                var buildJson = await buildResponse.Content.ReadAsStringAsync();
                var buildLookup = JsonSerializer.Deserialize(buildJson, TeamCityJsonContext.Default.MuteBuildLookup);

                resolvedBuildTypeId = buildLookup?.BuildTypeId ?? buildLookup?.BuildType?.Id;
                resolvedProjectId = buildLookup?.BuildType?.ProjectId;

                if (resolvedBuildTypeId is null || resolvedProjectId is null)
                    return $"ERROR: Unable to resolve the build configuration/project for build '{buildId}'.";
            }
            else if (!string.IsNullOrWhiteSpace(buildTypeId))
            {
                var buildTypeUrl = $"app/rest/buildTypes/id:{buildTypeId}?fields={Uri.EscapeDataString("projectId")}";
                var buildTypeResponse = await client.HttpClient.GetAsync(buildTypeUrl);

                if (buildTypeResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return "ERROR: Authentication failed — check the access token.";
                if (buildTypeResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return $"ERROR: Build type with ID '{buildTypeId}' was not found.";
                if (!buildTypeResponse.IsSuccessStatusCode)
                    return $"ERROR: TeamCity returned {(int)buildTypeResponse.StatusCode}: {buildTypeResponse.ReasonPhrase}";

                var buildTypeJson = await buildTypeResponse.Content.ReadAsStringAsync();
                var buildTypeSummary = JsonSerializer.Deserialize(buildTypeJson, TeamCityJsonContext.Default.BuildTypeSummary);
                resolvedProjectId = buildTypeSummary?.ProjectId;

                if (resolvedProjectId is null)
                    return $"ERROR: Unable to resolve the project for build type '{buildTypeId}'.";
            }

            var locatorParts = new List<string> { $"count:{count}" };
            if (!string.IsNullOrWhiteSpace(resolvedProjectId))
                locatorParts.Add($"affectedProject:(id:{resolvedProjectId})");

            var locator = string.Join(",", locatorParts);
            var fields = "count,mute(id," +
                         "assignment(text,user(username,name),timestamp)," +
                         "scope(project(id,name),buildTypes(buildType(id,name,projectName)),buildType(id,name,projectName))," +
                         "target(tests(test(id,name)),problems(problem(id)),anyProblem)," +
                         "resolution(type,time))";
            var url = $"app/rest/mutes?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return "ERROR: TeamCity returned 404 for the mutes lookup — check that the project/build type/build ID is valid.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var mutesResponse = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.MutesResponse);

            if (mutesResponse is null)
                return "ERROR: Unable to parse mutes response.";

            var fetchedCount = mutesResponse.Count ?? 0;
            var mutes = mutesResponse.Mute ?? [];

            if (!string.IsNullOrWhiteSpace(resolvedBuildTypeId))
                mutes = mutes.Where(m =>
                    m.Scope?.Project is not null ||
                    string.Equals(m.Scope?.BuildType?.Id, resolvedBuildTypeId, StringComparison.OrdinalIgnoreCase) ||
                    (m.Scope?.BuildTypes?.BuildType?.Any(bt =>
                        string.Equals(bt.Id, resolvedBuildTypeId, StringComparison.OrdinalIgnoreCase)) ?? false)
                ).ToList();

            if (!string.IsNullOrWhiteSpace(testNameFilter))
                mutes = mutes.Where(m =>
                    m.Target?.Tests?.Test?.Any(t =>
                        t.Name?.Contains(testNameFilter, StringComparison.OrdinalIgnoreCase) ?? false) ?? false
                ).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Mutes");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(buildId))
                sb.AppendLine($"**Build ID:** {buildId} (resolved to build type `{resolvedBuildTypeId}`, project `{resolvedProjectId}`)");
            else if (!string.IsNullOrWhiteSpace(buildTypeId))
                sb.AppendLine($"**Build Type Filter:** {buildTypeId} (project `{resolvedProjectId}`)");
            else if (!string.IsNullOrWhiteSpace(projectId))
                sb.AppendLine($"**Project Filter:** {projectId}");
            else
                sb.AppendLine("**Scope:** server-wide");
            if (!string.IsNullOrWhiteSpace(testNameFilter))
                sb.AppendLine($"**Test Name Filter:** {testNameFilter}");
            sb.AppendLine($"**Fetched:** {fetchedCount} (server locator, before filtering)");
            sb.AppendLine($"**Matched:** {mutes.Count}");
            sb.AppendLine();

            if (mutes.Count == 0)
            {
                sb.AppendLine("No mutes matched the given filters.");
                return sb.ToString();
            }

            foreach (var mute in mutes)
            {
                sb.AppendLine($"## Mute #{mute.Id}");
                sb.AppendLine();

                var testNames = mute.Target?.Tests?.Test?.Select(t => t.Name ?? "—").ToList();
                string targetText;
                if (testNames is { Count: > 0 })
                    targetText = string.Join(", ", testNames);
                else if (mute.Target?.AnyProblem == true)
                    targetText = "any build problem";
                else if (mute.Target?.Problems?.Problem is { Count: > 0 } problems)
                    targetText = $"build problem(s): {string.Join(", ", problems.Select(p => p.Id ?? "—"))}";
                else
                    targetText = "—";
                sb.AppendLine($"**Target:** {targetText}");

                string scopeText;
                if (mute.Scope?.Project is { } proj)
                    scopeText = $"project `{proj.Id}`{(string.IsNullOrWhiteSpace(proj.Name) ? "" : $" ({proj.Name})")} (and subprojects)";
                else if (mute.Scope?.BuildTypes?.BuildType is { Count: > 0 } scopedBuildTypes)
                    scopeText = string.Join(", ", scopedBuildTypes.Select(bt =>
                        $"`{bt.Id}`{(string.IsNullOrWhiteSpace(bt.Name) ? "" : $" ({bt.Name})")}"));
                else if (mute.Scope?.BuildType is { } scopedBuildType)
                    scopeText = $"`{scopedBuildType.Id}`{(string.IsNullOrWhiteSpace(scopedBuildType.Name) ? "" : $" ({scopedBuildType.Name})")}";
                else
                    scopeText = "—";
                sb.AppendLine($"**Scope:** {scopeText}");

                var reason = string.IsNullOrWhiteSpace(mute.Assignment?.Text)
                    ? "—"
                    : mute.Assignment.Text.Replace("\r\n", " ").Replace('\n', ' ');
                sb.AppendLine($"**Reason:** {reason}");

                var user = mute.Assignment?.User;
                var mutedBy = user is null
                    ? "—"
                    : string.IsNullOrWhiteSpace(user.Name)
                        ? user.Username ?? "—"
                        : $"{user.Name} ({user.Username})";
                var when = TeamCityFormat.FormatTcDate(mute.Assignment?.Timestamp);
                sb.AppendLine($"**Muted by / when:** {mutedBy} / {when}");

                var resolutionText = mute.Resolution?.Type switch
                {
                    "manually" => "manually",
                    "whenFixed" => "when fixed",
                    "atTime" => $"at {TeamCityFormat.FormatTcDate(mute.Resolution.Time)}",
                    var other => other ?? "—"
                };
                sb.AppendLine($"**Resolution:** {resolutionText}");
                sb.AppendLine();
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to list mutes — {ex.Message}";
        }
    }
}
