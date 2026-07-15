namespace TeamCityMcpTools.Models;

public class DependencyBuildListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("build")]
    public List<DependencyBuildNode>? Build { get; set; }
}

public class DependencyBuildNode
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("buildTypeId")]
    public string? BuildTypeId { get; set; }

    [JsonPropertyName("buildType")]
    public DependencyBuildTypeRef? BuildType { get; set; }
}

public class DependencyBuildTypeRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }
}

public class SnapshotDependenciesWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("snapshot-dependency")]
    public List<SnapshotDependencyEntry>? SnapshotDependency { get; set; }
}

public class SnapshotDependencyEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("source-buildType")]
    public DependencyBuildTypeRef? SourceBuildType { get; set; }
}

public class BuildDependenciesEnvelope
{
    [JsonPropertyName("snapshot-dependencies")]
    public DependencyBuildListResponse? SnapshotDependencies { get; set; }

    [JsonPropertyName("artifact-dependencies")]
    public DependencyBuildListResponse? ArtifactDependencies { get; set; }
}

public class ArtifactDependenciesWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("artifact-dependency")]
    public List<ArtifactDependencyEntry>? ArtifactDependency { get; set; }
}

public class ArtifactDependencyEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("source-buildType")]
    public DependencyBuildTypeRef? SourceBuildType { get; set; }

    [JsonPropertyName("properties")]
    public ArtifactDependencyPropertiesWrapper? Properties { get; set; }
}

public class ArtifactDependencyPropertiesWrapper
{
    [JsonPropertyName("property")]
    public List<ArtifactDependencyProperty>? Property { get; set; }
}

public class ArtifactDependencyProperty
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
