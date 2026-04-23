namespace TeamCityMcpTools.Models;

public class BuildListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("build")]
    public List<BuildSummary>? Build { get; set; }
}

public class BuildSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("finishDate")]
    public string? FinishDate { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }
}

public class BuildDetails : BuildSummary
{
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("buildType")]
    public BuildTypeInfo? BuildType { get; set; }

    [JsonPropertyName("agent")]
    public AgentInfo? Agent { get; set; }

    [JsonPropertyName("triggered")]
    public TriggeredInfo? Triggered { get; set; }

    [JsonPropertyName("revisions")]
    public RevisionsWrapper? Revisions { get; set; }

    [JsonPropertyName("problemOccurrences")]
    public ProblemOccurrencesWrapper? ProblemOccurrences { get; set; }
}

public class BuildTypeInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }
}

public class AgentInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class TriggeredInfo
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("user")]
    public TriggeredUser? User { get; set; }
}

public class TriggeredUser
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class RevisionsWrapper
{
    [JsonPropertyName("revision")]
    public List<RevisionInfo>? Revision { get; set; }
}

public class RevisionInfo
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("vcsBranch")]
    public string? VcsBranch { get; set; }
}

public class ProblemOccurrencesWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("problemOccurrence")]
    public List<ProblemOccurrence>? ProblemOccurrence { get; set; }
}

public class ProblemOccurrence
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}
