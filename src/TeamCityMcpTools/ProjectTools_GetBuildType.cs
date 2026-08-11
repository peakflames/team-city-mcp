namespace TeamCityMcpTools;

public partial class ProjectTools
{
    [McpServerTool(Name = TeamCityToolNames.GetBuildType),
        Description(
            "Gets full details for a specific TeamCity build configuration, including template linkage, general " +
            "settings, VCS roots, triggers, build steps, agent requirements, and snapshot/artifact dependencies. " +
            "Every section marks each item as 'own' (defined directly on this build type) or 'inherited' (defined " +
            "on an attached template), so inheritance is visible without inspecting the template separately. Also " +
            "lists Build Features (own/inherited/disabled) in compact form — use 'teamcity_get_build_type_features' " +
            "for full feature property detail, including the Matrix Build feature. Does NOT include configuration " +
            "parameters — use 'teamcity_get_build_type_parameters' for those. Returns a markdown document; resolves " +
            "entirely from the build type ID, no build ID needed.")]
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
            var fields = "id,name,description,projectId,projectName,paused,webUrl,templateFlag,templates(buildType(id,name)),settings(property(name,value,inherited)),vcs-root-entries(vcs-root-entry(id,inherited,checkout-rules,vcs-root(id,name,vcsName))),triggers(trigger(id,type,inherited,properties(property(name,value)))),steps(step(id,name,type,disabled,inherited,properties(property(name,value)))),agentRequirements(agentRequirement(id,type,disabled,properties(property(name,value)))),snapshot-dependencies(snapshot-dependency(id,inherited,source-buildType(id,name,projectName))),artifact-dependencies(artifact-dependency(id,disabled,inherited,source-buildType(id,name,projectName),properties(property(name,value)))),features(feature(id,type,disabled,inherited))";
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
            sb.AppendLine($"| Is Template | {(bt.TemplateFlag == true ? "Yes" : "No")} |");
            sb.AppendLine($"| URL | {bt.WebUrl ?? "—"} |");
            sb.AppendLine();

            var templates = bt.Templates?.BuildType;
            sb.AppendLine("## Templates");
            sb.AppendLine();
            if (templates is { Count: > 0 })
            {
                sb.AppendLine("This build configuration inherits settings from the following template(s):");
                sb.AppendLine();
                sb.AppendLine("| Template ID | Name |");
                sb.AppendLine("|-------------|------|");
                foreach (var template in templates)
                    sb.AppendLine($"| {template.Id} | {template.Name ?? "—"} |");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("No template attached — this build configuration does not inherit from a template.");
                sb.AppendLine();
            }

            var settings = bt.Settings?.Property;
            sb.AppendLine("## General Settings");
            sb.AppendLine();
            if (settings is { Count: > 0 })
            {
                sb.AppendLine("| Name | Value | Source |");
                sb.AppendLine("|------|-------|--------|");
                foreach (var setting in settings.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var source = setting.Inherited == true ? "inherited" : "own";
                    sb.AppendLine($"| {setting.Name} | {setting.Value} | {source} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("No general settings reported.");
                sb.AppendLine();
            }

            var entries = bt.VcsRoots?.VcsRootEntry;
            if (entries is { Count: > 0 })
            {
                sb.AppendLine("## VCS Roots");
                sb.AppendLine();
                sb.AppendLine("| Entry ID | VCS Root ID | Name | Type | Source | Checkout Rules |");
                sb.AppendLine("|----------|-------------|------|------|--------|----------------|");
                foreach (var entry in entries)
                {
                    var vcs = entry.VcsRoot;
                    var source = entry.Inherited == true ? "inherited" : "own";
                    var checkoutRules = string.IsNullOrWhiteSpace(entry.CheckoutRules) ? "—" : entry.CheckoutRules;
                    sb.AppendLine($"| {entry.Id} | {vcs?.Id ?? "—"} | {vcs?.Name ?? "—"} | {vcs?.VcsName ?? "—"} | {source} | {checkoutRules} |");
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

            sb.AppendLine("## Dependencies");
            sb.AppendLine();

            var snapshotDeps = bt.SnapshotDependencies?.SnapshotDependency;
            sb.AppendLine("### Snapshot");
            sb.AppendLine();
            if (snapshotDeps is { Count: > 0 })
            {
                sb.AppendLine("| Source Build Type | ID | Project | Source |");
                sb.AppendLine("|--------------------|-----|---------|--------|");
                foreach (var dep in snapshotDeps)
                {
                    var source = dep.SourceBuildType;
                    var origin = dep.Inherited == true ? "inherited" : "own";
                    sb.AppendLine($"| {source?.Name ?? "—"} | {source?.Id ?? "—"} | {source?.ProjectName ?? "—"} | {origin} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("No snapshot dependencies configured.");
                sb.AppendLine();
            }

            var artifactDeps = bt.ArtifactDependencies?.ArtifactDependency;
            sb.AppendLine("### Artifact");
            sb.AppendLine();
            if (artifactDeps is { Count: > 0 })
            {
                sb.AppendLine("| Source Build Type | ID | Project | Path Rules | Revision | Disabled | Source |");
                sb.AppendLine("|--------------------|-----|---------|------------|----------|----------|--------|");
                foreach (var dep in artifactDeps)
                {
                    var source = dep.SourceBuildType;
                    var props = dep.Properties?.Property;
                    var pathRules = props?.FirstOrDefault(p => p.Name == "pathRules")?.Value ?? "—";
                    var revisionName = props?.FirstOrDefault(p => p.Name == "revisionName")?.Value;
                    var revisionValue = props?.FirstOrDefault(p => p.Name == "revisionValue")?.Value;
                    var revision = revisionName is null ? "—" : string.IsNullOrWhiteSpace(revisionValue) ? revisionName : $"{revisionName} ({revisionValue})";
                    var disabled = dep.Disabled == true ? "Yes" : "No";
                    var origin = dep.Inherited == true ? "inherited" : "own";
                    sb.AppendLine($"| {source?.Name ?? "—"} | {source?.Id ?? "—"} | {source?.ProjectName ?? "—"} | {pathRules} | {revision} | {disabled} | {origin} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("No artifact dependencies configured.");
                sb.AppendLine();
            }

            var triggers = bt.Triggers?.Trigger;
            sb.AppendLine("## Triggers");
            sb.AppendLine();
            if (triggers is { Count: > 0 })
            {
                foreach (var trigger in triggers)
                {
                    var origin = trigger.Inherited == true ? " *(inherited)*" : "";
                    sb.AppendLine($"### {trigger.Type ?? trigger.Id ?? "Unknown"} (`{trigger.Id}`){origin}");
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
                    var origin = step.Inherited == true ? " *(inherited)*" : "";
                    sb.AppendLine($"### Step {i + 1}: {(string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name)}{disabled}{origin}");
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

            var features = bt.Features?.Feature;
            sb.AppendLine("## Build Features");
            sb.AppendLine();
            if (features is { Count: > 0 })
            {
                sb.AppendLine("*Compact view — use teamcity_get_build_type_features for full property detail.*");
                sb.AppendLine();
                sb.AppendLine("| Type | ID | Source | Disabled |");
                sb.AppendLine("|------|-----|--------|----------|");
                foreach (var feature in features)
                {
                    var origin = feature.Inherited == true ? "inherited" : "own";
                    var disabled = feature.Disabled == true ? "Yes" : "No";
                    sb.AppendLine($"| {feature.Type ?? "—"} | {feature.Id ?? "—"} | {origin} | {disabled} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("No build features configured.");
                sb.AppendLine();
            }

            return TeamCityFormat.Clamp(sb.ToString());
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to get build type — {ex.Message}";
        }
    }
}
