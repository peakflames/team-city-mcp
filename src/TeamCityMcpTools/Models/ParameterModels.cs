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
