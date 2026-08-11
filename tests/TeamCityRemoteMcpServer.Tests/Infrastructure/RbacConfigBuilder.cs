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
}
