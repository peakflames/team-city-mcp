namespace TeamCityRemoteMcpServer.Auth;

public static class OAuthScopes
{
    public const string Read = "teamcity:read";

    /// <summary>Reserved for Phase 2; no tool requires it yet.</summary>
    public const string Write = "teamcity:write";

    /// <summary>Authorization policy name gating POST /mcp.</summary>
    public const string ReadPolicy = "TeamCityRead";
}
