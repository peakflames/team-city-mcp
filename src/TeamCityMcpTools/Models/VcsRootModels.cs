namespace TeamCityMcpTools.Models;

public class VcsRootListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("vcs-root")]
    public List<VcsRootSummary>? VcsRoot { get; set; }
}

public class VcsRootSummary
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class VcsRootDetails
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vcsName")]
    public string? VcsName { get; set; }

    [JsonPropertyName("project")]
    public VcsRootProjectRef? Project { get; set; }

    [JsonPropertyName("properties")]
    public VcsRootPropertiesWrapper? Properties { get; set; }
}

public class VcsRootProjectRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class VcsRootPropertiesWrapper
{
    [JsonPropertyName("property")]
    public List<VcsRootProperty>? Property { get; set; }
}

public class VcsRootProperty
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
