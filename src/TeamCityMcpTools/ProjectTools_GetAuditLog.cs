namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_audit_log"),
        Description(
            "Gets TeamCity audit log entries (configuration changes, permission changes, etc.), " +
            "optionally scoped to a build type or project. Requires an access token with audit-read " +
            "permission — degrades gracefully with a clear error if the token lacks it.")]
    public async Task<string> GetAuditLog(
        [Description("Optional build type ID to scope the audit log to.")]
        string? buildTypeId = null,

        [Description("Optional project ID to scope the audit log to (includes sub-projects).")]
        string? affectedProjectId = null,

        [Description("Maximum number of audit events to return. Defaults to 50.")]
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
            var locatorParts = new List<string> { $"count:{count}" };
            if (!string.IsNullOrWhiteSpace(buildTypeId))
                locatorParts.Add($"buildType:{buildTypeId}");
            if (!string.IsNullOrWhiteSpace(affectedProjectId))
                locatorParts.Add($"affectedProject:{affectedProjectId}");

            var locator = string.Join(",", locatorParts);
            var fields = "count,auditEvent(action(name),timestamp,user(username),comment)";
            var url = $"app/rest/audit?locator={Uri.EscapeDataString(locator)}&fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return "ERROR: Access denied — the access token does not have permission to read the audit log " +
                       "(audit read requires elevated/system-admin permissions in TeamCity).";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var audit = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.AuditEventsResponse);

            if (audit is null)
                return "ERROR: Unable to parse audit log response.";

            var sb = new StringBuilder();
            sb.AppendLine("# Audit Log");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(buildTypeId))
                sb.AppendLine($"**Build Type Filter:** {buildTypeId}");
            if (!string.IsNullOrWhiteSpace(affectedProjectId))
                sb.AppendLine($"**Project Filter:** {affectedProjectId}");
            sb.AppendLine($"**Count:** {audit.Count ?? 0}");
            sb.AppendLine();

            if (audit.AuditEvent is not { Count: > 0 } events)
            {
                sb.AppendLine("No audit events found matching the given filters.");
                return sb.ToString();
            }

            sb.AppendLine("| When | Action | User | Comment |");
            sb.AppendLine("|------|--------|------|---------|");

            foreach (var evt in events)
            {
                var when = TeamCityFormat.FormatTcDate(evt.Timestamp);
                var user = evt.User?.Username ?? "system";
                var comment = evt.Comment ?? string.Empty;
                sb.AppendLine($"| {when} | {evt.Action?.Name} | {user} | {comment} |");
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get audit log — {ex.Message}";
        }
    }
}
