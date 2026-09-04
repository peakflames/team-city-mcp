namespace TeamCityRemoteMcpServer.Auth;

/// <summary>
/// Replaces the policy's former inline <c>RequireAssertion</c> scope check. The assertion form had
/// no way to reach <see cref="McpAuthOptions.RequireScope"/>: an AuthorizationHandlerContext exposes
/// no service provider, so config can only be consulted from a handler resolved out of DI.
/// </summary>
public sealed class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(string requiredScope)
    {
        RequiredScope = requiredScope;
    }

    public string RequiredScope { get; }
}
