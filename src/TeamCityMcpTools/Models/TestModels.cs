namespace TeamCityMcpTools.Models;

public class TestOccurrencesResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("passed")]
    public int? Passed { get; set; }

    [JsonPropertyName("failed")]
    public int? Failed { get; set; }

    [JsonPropertyName("ignored")]
    public int? Ignored { get; set; }

    [JsonPropertyName("muted")]
    public int? Muted { get; set; }

    [JsonPropertyName("testOccurrence")]
    public List<TestOccurrence>? TestOccurrence { get; set; }
}

public class TestOccurrence
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("newFailure")]
    public bool? NewFailure { get; set; }

    [JsonPropertyName("muted")]
    public bool? Muted { get; set; }

    [JsonPropertyName("firstFailed")]
    public FirstFailedWrapper? FirstFailed { get; set; }

    [JsonPropertyName("build")]
    public TestBuildRef? Build { get; set; }
}

public class FirstFailedWrapper
{
    [JsonPropertyName("build")]
    public TestBuildRef? Build { get; set; }
}

public class TestBuildRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }

    [JsonPropertyName("buildType")]
    public BuildTypeInfo? BuildType { get; set; }
}
