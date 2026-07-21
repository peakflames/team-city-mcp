namespace TeamCityMcpTools.Models;

public class BuildProblemsResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("problemOccurrence")]
    public List<BuildProblemOccurrence>? ProblemOccurrence { get; set; }
}

public class BuildProblemOccurrence
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("identity")]
    public string? Identity { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}
