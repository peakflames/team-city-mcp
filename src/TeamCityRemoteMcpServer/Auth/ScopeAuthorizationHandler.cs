namespace TeamCityRemoteMcpServer.Auth;

/// <summary>
/// Satisfies <see cref="ScopeRequirement"/> when the caller holds the scope, or unconditionally when
/// <see cref="McpAuthOptions.RequireScope"/> is false.
///
/// Dropping the scope assertion is not a loss of authorization. An authorization server with no
/// custom-scope capability can only mint org-wide OIDC scopes, so a scope value there conveys
/// nothing resource-specific — it would be a string the resource server checks against itself. The
/// authorization decision that matters is made downstream by the RBAC gate against the upstream
/// system's own permission model, which grants nothing on the strength of a scope claim.
/// </summary>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    private readonly IOptions<McpAuthOptions> _authOptions;

    public ScopeAuthorizationHandler(IOptions<McpAuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        if (!_authOptions.Value.RequireScope
            || ScopeClaimHelper.HasScope(context.User, requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
