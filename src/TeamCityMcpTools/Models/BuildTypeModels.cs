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
