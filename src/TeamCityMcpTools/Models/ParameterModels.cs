namespace TeamCityMcpTools.Models;

public class ParameterListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("property")]
    public List<ParameterEntry>? Property { get; set; }
}

public class ParameterEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class BuildTypeParameterListResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("property")]
    public List<BuildTypeParameterEntry>? Property { get; set; }
}

public class BuildTypeParameterEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("inherited")]
    public bool? Inherited { get; set; }

    [JsonPropertyName("type")]
    public ParameterTypeSpec? Type { get; set; }
}

public class ParameterTypeSpec
{
    [JsonPropertyName("rawValue")]
    public string? RawValue { get; set; }
}
