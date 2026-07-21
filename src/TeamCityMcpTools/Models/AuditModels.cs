namespace TeamCityMcpTools.Models;

public class AuditEventsResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("auditEvent")]
    public List<AuditEventEntry>? AuditEvent { get; set; }
}

public class AuditEventEntry
{
    [JsonPropertyName("action")]
    public AuditActionRef? Action { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("user")]
    public AuditUserRef? User { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

public class AuditActionRef
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class AuditUserRef
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
