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
    // TeamCity's REST API returns user ids as a JSON number (unlike project/buildType ids, which
    // are strings) — matches the int Id convention already used for build/change/mute/test ids
    // elsewhere in TeamCityMcpTools.Models.
    [JsonPropertyName("id")]
    public int? Id { get; set; }
}

public sealed class RbacPermissionCountResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
