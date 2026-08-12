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

    [JsonPropertyName("percentageComplete")]
    public int? PercentageComplete { get; set; }

    [JsonPropertyName("agent")]
    public AgentInfo? Agent { get; set; }

    [JsonPropertyName("buildType")]
    public BuildTypeInfo? BuildType { get; set; }
}

public class BuildDetails : BuildSummary
{
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("personal")]
    public bool? Personal { get; set; }

    [JsonPropertyName("composite")]
    public bool? Composite { get; set; }

    [JsonPropertyName("triggered")]
    public TriggeredInfo? Triggered { get; set; }

    [JsonPropertyName("revisions")]
    public RevisionsWrapper? Revisions { get; set; }

    [JsonPropertyName("problemOccurrences")]
    public ProblemOccurrencesWrapper? ProblemOccurrences { get; set; }
}

public class BuildTypeInfo
{
    /// <summary>Not requested by every consumer's <c>fields=</c> string — null wherever it wasn't
    /// asked for. RBAC-scoped consumers (<c>teamcity_search_builds</c>, <c>teamcity_get_test_history</c>)
    /// request it explicitly, since <see cref="ProjectName"/> is a display name and must never key a
    /// visibility check.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Same caveat as <see cref="Id"/> — only populated when a consumer's <c>fields=</c>
    /// string requests <c>projectId</c> on the buildType.</summary>
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

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

    [JsonPropertyName("vcsBranchName")]
    public string? VcsBranch { get; set; }

    [JsonPropertyName("vcs-root-instance")]
    public VcsRootInstanceRef? VcsRootInstance { get; set; }
}

public class VcsRootInstanceRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("vcs-root-id")]
    public string? VcsRootId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vcsName")]
    public string? VcsName { get; set; }
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

public class QueuedBuildListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("build")]
    public List<QueuedBuildSummary>? Build { get; set; }
}

public class QueuedBuildSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildType")]
    public QueuedBuildTypeInfo? BuildType { get; set; }

    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }

    [JsonPropertyName("triggered")]
    public TriggeredInfo? Triggered { get; set; }

    [JsonPropertyName("waitReason")]
    public string? WaitReason { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }
}

public class QueuedBuildTypeInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }
}

public class BuildStatusSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("statusText")]
    public string? StatusText { get; set; }

    [JsonPropertyName("percentageComplete")]
    public int? PercentageComplete { get; set; }

    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }
}

public class ServerInfo
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    [JsonPropertyName("currentTime")]
    public string? CurrentTime { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }
}
