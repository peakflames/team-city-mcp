namespace TeamCityMcpTools.Models;

public class BuildTypeListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("buildType")]
    public List<BuildTypeSummary>? BuildType { get; set; }
}

public class BuildTypeSummary
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }
}

public class BuildTypeDetails : BuildTypeSummary
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("paused")]
    public bool? Paused { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }

    [JsonPropertyName("vcsRoots")]
    public VcsRootsWrapper? VcsRoots { get; set; }

    [JsonPropertyName("triggers")]
    public TriggersWrapper? Triggers { get; set; }

    [JsonPropertyName("steps")]
    public StepsWrapper? Steps { get; set; }

    [JsonPropertyName("agentRequirements")]
    public AgentRequirementsWrapper? AgentRequirements { get; set; }
}

public class StepsWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("step")]
    public List<StepInfo>? Step { get; set; }
}

public class StepInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("properties")]
    public StepPropertiesWrapper? Properties { get; set; }
}

public class StepPropertiesWrapper
{
    [JsonPropertyName("property")]
    public List<StepProperty>? Property { get; set; }
}

public class StepProperty
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class AgentRequirementsWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("agentRequirement")]
    public List<AgentRequirement>? AgentRequirement { get; set; }
}

public class AgentRequirement
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("properties")]
    public AgentRequirementPropertiesWrapper? Properties { get; set; }
}

public class AgentRequirementPropertiesWrapper
{
    [JsonPropertyName("property")]
    public List<AgentRequirementProperty>? Property { get; set; }
}

public class AgentRequirementProperty
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class VcsRootsWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("vcsRootEntry")]
    public List<VcsRootEntry>? VcsRootEntry { get; set; }
}

public class VcsRootEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("vcsRoot")]
    public VcsRootInfo? VcsRoot { get; set; }
}

public class VcsRootInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vcsName")]
    public string? VcsName { get; set; }
}

public class TriggersWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("trigger")]
    public List<TriggerInfo>? Trigger { get; set; }
}

public class TriggerInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("properties")]
    public TriggerPropertiesWrapper? Properties { get; set; }
}

public class TriggerPropertiesWrapper
{
    [JsonPropertyName("property")]
    public List<TriggerProperty>? Property { get; set; }
}

public class TriggerProperty
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
