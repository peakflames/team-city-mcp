namespace TeamCityMcpTools.Models;

public class ProjectListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("project")]
    public List<ProjectSummary>? Project { get; set; }
}

public class ProjectSummary
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parentProjectId")]
    public string? ParentProjectId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class ProjectDetails
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parentProject")]
    public ParentProjectRef? ParentProject { get; set; }

    [JsonPropertyName("projects")]
    public ProjectListResponse? Projects { get; set; }

    [JsonPropertyName("buildTypes")]
    public BuildTypeListResponse? BuildTypes { get; set; }
}

public class ParentProjectRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
