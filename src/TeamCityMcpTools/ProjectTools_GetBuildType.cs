namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = "teamcity_get_build_type"),
        Description(
            "Gets full details for a specific TeamCity build configuration, including VCS root info and a direct web URL. " +
            "Returns a markdown document with build type metadata and VCS roots.")]
    public async Task<string> GetBuildType(
        [Description("The TeamCity build type ID (e.g., 'MyProject_Build').")]
        string buildTypeId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
            return $"ERROR: {clientResult.Errors.First().Message}";

        var client = clientResult.Value;

        try
        {
            var fields = "id,name,description,projectId,projectName,paused,webUrl,vcsRoots(vcsRootEntry(id,vcsRoot(id,name,vcsName))),triggers(trigger(id,type,properties(property(name,value)))),steps(step(id,name,type,disabled,properties(property(name,value)))),agentRequirements(agentRequirement(id,type,disabled,properties(property(name,value))))";
            var url = $"app/rest/buildTypes/id:{buildTypeId}?fields={Uri.EscapeDataString(fields)}";

            var response = await client.HttpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "ERROR: Authentication failed — check the access token.";
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return $"ERROR: Build type with ID '{buildTypeId}' was not found.";
            if (!response.IsSuccessStatusCode)
                return $"ERROR: TeamCity returned {(int)response.StatusCode}: {response.ReasonPhrase}";

            var json = await response.Content.ReadAsStringAsync();
            var bt = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.BuildTypeDetails);

            if (bt is null)
                return $"ERROR: Unable to parse build type details for ID '{buildTypeId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Build Configuration: {bt.Name}");
            sb.AppendLine();
            sb.AppendLine("## Details");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");
            sb.AppendLine($"| ID | {bt.Id} |");
            sb.AppendLine($"| Name | {bt.Name} |");
            sb.AppendLine($"| Description | {(string.IsNullOrWhiteSpace(bt.Description) ? "—" : bt.Description)} |");
            sb.AppendLine($"| Project ID | {bt.ProjectId} |");
            sb.AppendLine($"| Project Name | {bt.ProjectName} |");
            sb.AppendLine($"| Paused | {(bt.Paused == true ? "Yes" : "No")} |");
            sb.AppendLine($"| URL | {bt.WebUrl ?? "—"} |");
            sb.AppendLine();

            var entries = bt.VcsRoots?.VcsRootEntry;
            if (entries is { Count: > 0 })
            {
                sb.AppendLine("## VCS Roots");
                sb.AppendLine();
                sb.AppendLine("| Entry ID | VCS Root ID | Name | Type |");
                sb.AppendLine("|----------|-------------|------|------|");
                foreach (var entry in entries)
                {
                    var vcs = entry.VcsRoot;
                    sb.AppendLine($"| {entry.Id} | {vcs?.Id ?? "—"} | {vcs?.Name ?? "—"} | {vcs?.VcsName ?? "—"} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("## VCS Roots");
                sb.AppendLine();
                sb.AppendLine("No VCS roots configured.");
                sb.AppendLine();
            }

            var triggers = bt.Triggers?.Trigger;
            sb.AppendLine("## Triggers");
            sb.AppendLine();
            if (triggers is { Count: > 0 })
            {
                foreach (var trigger in triggers)
                {
                    sb.AppendLine($"### {trigger.Type ?? trigger.Id ?? "Unknown"} (`{trigger.Id}`)");
                    sb.AppendLine();
                    var props = trigger.Properties?.Property;
                    if (props is { Count: > 0 })
                    {
                        sb.AppendLine("| Property | Value |");
                        sb.AppendLine("|----------|-------|");
                        foreach (var prop in props)
                            sb.AppendLine($"| {prop.Name} | {prop.Value} |");
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine("No properties.");
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine("No triggers configured.");
                sb.AppendLine();
            }

            var steps = bt.Steps?.Step;
            sb.AppendLine("## Build Steps");
            sb.AppendLine();
            if (steps is { Count: > 0 })
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    var disabled = step.Disabled == true ? " *(disabled)*" : "";
                    sb.AppendLine($"### Step {i + 1}: {(string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name)}{disabled}");
                    sb.AppendLine();
                    sb.AppendLine($"- **Type**: {step.Type ?? "—"}");
                    sb.AppendLine($"- **ID**: {step.Id ?? "—"}");
                    var stepProps = step.Properties?.Property;
                    if (stepProps is { Count: > 0 })
                    {
                        sb.AppendLine();
                        sb.AppendLine("| Property | Value |");
                        sb.AppendLine("|----------|-------|");
                        foreach (var prop in stepProps)
                            sb.AppendLine($"| {prop.Name} | {prop.Value} |");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("No build steps configured.");
                sb.AppendLine();
            }

            var agentReqs = bt.AgentRequirements?.AgentRequirement;
            sb.AppendLine("## Agent Requirements");
            sb.AppendLine();
            if (agentReqs is { Count: > 0 })
            {
                foreach (var req in agentReqs)
                {
                    var disabled = req.Disabled == true ? " *(disabled)*" : "";
                    sb.AppendLine($"### {req.Id ?? "Requirement"}{disabled}");
                    sb.AppendLine();
                    sb.AppendLine($"- **Type**: {req.Type ?? "—"}");
                    var reqProps = req.Properties?.Property;
                    if (reqProps is { Count: > 0 })
                    {
                        sb.AppendLine();
                        sb.AppendLine("| Property | Value |");
                        sb.AppendLine("|----------|-------|");
                        foreach (var prop in reqProps)
                            sb.AppendLine($"| {prop.Name} | {prop.Value} |");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("No agent requirements configured.");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build type — {ex.Message}";
        }
    }
}
