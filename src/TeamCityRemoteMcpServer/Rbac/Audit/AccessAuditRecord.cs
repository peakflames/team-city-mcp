namespace TeamCityRemoteMcpServer.Rbac.Audit;

public enum AccessDecision
{
    Allow,
    Deny,
}

/// <summary>
/// One record per gated <c>tools/call</c> — the artifact a security reviewer checks for "who did
/// what" in place of TeamCity's own audit log, which can only ever name the shared service account.
/// Extends the design doc's <c>(timestamp, OAuth identity, tool, resource, permission, decision,
/// TeamCity user)</c> shape with <c>ClientId</c> and <c>Jti</c> — Phase 1 already issues both, and
/// <c>Jti</c> is what correlates a denial to a specific token during an investigation.
///
/// Named <c>AccessAuditRecord</c>/<c>McpAccessAuditSink</c>, not <c>AuditRecord</c>/<c>McpAuditSink</c>
/// — <c>Models/AuditModels.cs</c> and <c>teamcity_get_audit_log</c> already own the bare "audit"
/// name in this repo for TeamCity's own audit log.
/// </summary>
public sealed record AccessAuditRecord(
    DateTimeOffset Timestamp,
    string? OAuthSubject,
    string? OAuthClientId,
    string? Jti,
    string? IdentityClaimValue,
    string? TeamCityUserId,
    string ToolName,
    string? Resource,
    string? Permission,
    AccessDecision Decision,
    int? FilteredOutCount,
    long ElapsedMilliseconds);
