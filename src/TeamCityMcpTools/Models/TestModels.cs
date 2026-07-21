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

/// <summary>
/// Whole-build test totals plus composite/chain metadata, from
/// builds/id:X?fields=composite,number,buildType(id,name),testOccurrences(...),snapshot-dependencies(...).
/// The top-level counts on a testOccurrences *list* response are page-scoped (limited by `count`), while these
/// totals reflect the entire build (and, for a composite build, its whole chain) — see teamcity_get_build_tests.
/// </summary>
public class BuildCompositeSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("composite")]
    public bool? Composite { get; set; }

    [JsonPropertyName("buildType")]
    public BuildTypeInfo? BuildType { get; set; }

    [JsonPropertyName("testOccurrences")]
    public TestOccurrenceTotals? TestOccurrences { get; set; }

    [JsonPropertyName("snapshot-dependencies")]
    public ChainPartListResponse? SnapshotDependencies { get; set; }
}

public class TestOccurrenceTotals
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

    [JsonPropertyName("newFailed")]
    public int? NewFailed { get; set; }
}

public class ChainPartListResponse
{
    [JsonPropertyName("build")]
    public List<ChainPartBuildRef>? Build { get; set; }
}

public class ChainPartBuildRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("composite")]
    public bool? Composite { get; set; }

    [JsonPropertyName("buildType")]
    public BuildTypeInfo? BuildType { get; set; }
}

/// <summary>Per-part totals lookup: builds/id:X?fields=testOccurrences(...).</summary>
public class TestOccurrenceTotalsEnvelope
{
    [JsonPropertyName("testOccurrences")]
    public TestOccurrenceTotals? TestOccurrences { get; set; }
}
