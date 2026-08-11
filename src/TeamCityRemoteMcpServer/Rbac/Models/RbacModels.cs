namespace TeamCityRemoteMcpServer.Rbac.Models;

public sealed class RbacUserLookupResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("user")]
    public List<RbacUserRef>? User { get; set; }
}

public sealed class RbacUserRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public sealed class RbacPermissionCountResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
