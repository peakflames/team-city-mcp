namespace TeamCityMcpTools.Models;

public class MutesResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("mute")]
    public List<MuteEntry>? Mute { get; set; }
}

public class MuteEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("assignment")]
    public MuteAssignment? Assignment { get; set; }

    [JsonPropertyName("scope")]
    public MuteScope? Scope { get; set; }

    [JsonPropertyName("target")]
    public MuteTarget? Target { get; set; }

    [JsonPropertyName("resolution")]
    public MuteResolution? Resolution { get; set; }
}

public class MuteAssignment
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("user")]
    public MuteUserRef? User { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public class MuteUserRef
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class MuteScope
{
    [JsonPropertyName("project")]
    public ProjectSummary? Project { get; set; }

    [JsonPropertyName("buildTypes")]
    public MuteBuildTypesWrapper? BuildTypes { get; set; }

    [JsonPropertyName("buildType")]
    public BuildTypeSummary? BuildType { get; set; }
}

public class MuteBuildTypesWrapper
{
    [JsonPropertyName("buildType")]
    public List<BuildTypeSummary>? BuildType { get; set; }
}

public class MuteTarget
{
    [JsonPropertyName("tests")]
    public MuteTestsWrapper? Tests { get; set; }

    [JsonPropertyName("problems")]
    public MuteProblemsWrapper? Problems { get; set; }

    [JsonPropertyName("anyProblem")]
    public bool? AnyProblem { get; set; }
}

public class MuteTestsWrapper
{
    [JsonPropertyName("test")]
    public List<MuteTestRef>? Test { get; set; }
}

public class MuteTestRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class MuteProblemsWrapper
{
    [JsonPropertyName("problem")]
    public List<MuteProblemRef>? Problem { get; set; }
}

public class MuteProblemRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class MuteResolution
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }
}

public class MuteBuildLookup
{
    [JsonPropertyName("buildTypeId")]
    public string? BuildTypeId { get; set; }

    [JsonPropertyName("buildType")]
    public MuteBuildTypeRef? BuildType { get; set; }
}

public class MuteBuildTypeRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }
}
