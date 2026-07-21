namespace TeamCityMcpTools.Models;

public class ArtifactChildrenResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("file")]
    public List<ArtifactFileEntry>? File { get; set; }
}

public class ArtifactFileEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("modificationTime")]
    public string? ModificationTime { get; set; }

    [JsonPropertyName("children")]
    public ArtifactChildrenRef? Children { get; set; }
}

public class ArtifactChildrenRef
{
    [JsonPropertyName("href")]
    public string? Href { get; set; }
}
