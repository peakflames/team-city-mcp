namespace TeamCityMcpTools.Models;

public class ChangesResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("change")]
    public List<ChangeEntry>? Change { get; set; }
}

public class ChangeEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("files")]
    public ChangeFilesWrapper? Files { get; set; }
}

public class ChangeFilesWrapper
{
    [JsonPropertyName("file")]
    public List<ChangeFileEntry>? File { get; set; }
}

public class ChangeFileEntry
{
    [JsonPropertyName("file")]
    public string? FilePath { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }
}
