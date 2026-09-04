namespace TeamCityRemoteMcpServer.Tests;

public static class RbacConfigBuilder
{
    public static McpServerFactory WithRbacEnabled(this McpServerFactory factory, bool auditOnly = false, string identityClaim = "email")
    {
        return factory
            .With("Rbac:Enabled", "true")
            .With("Rbac:AuditOnly", auditOnly ? "true" : "false")
            .With("Rbac:IdentityClaim", identityClaim);
    }

    /// <summary>RBAC with identity resolved from the authorization server's <c>/userinfo</c> endpoint
    /// instead of a token claim — the Okta org-authorization-server shape.</summary>
    public static McpServerFactory WithRbacUserInfoIdentity(this McpServerFactory factory, bool auditOnly = false)
    {
        return factory
            .With("Rbac:Enabled", "true")
            .With("Rbac:AuditOnly", auditOnly ? "true" : "false")
            .With("Rbac:IdentitySource", "UserInfo");
    }
}
