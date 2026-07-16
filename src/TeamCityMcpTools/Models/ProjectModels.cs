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

    [JsonPropertyName("templates")]
    public TemplatesWrapper? Templates { get; set; }
}

public class ParentProjectRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class ProjectFeaturesWrapper
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("projectFeature")]
    public List<ProjectFeatureEntry>? ProjectFeature { get; set; }
}

public class ProjectFeatureEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("inherited")]
    public bool? Inherited { get; set; }

    [JsonPropertyName("properties")]
    public ProjectFeaturePropertiesWrapper? Properties { get; set; }
}

public class ProjectFeaturePropertiesWrapper
{
    [JsonPropertyName("property")]
    public List<ProjectFeatureProperty>? Property { get; set; }
}

public class ProjectFeatureProperty
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
