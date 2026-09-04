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
/// <see cref="Decision"/> and <see cref="DecisionReason"/> always carry the gate's true verdict,
/// regardless of <c>Rbac:AuditOnly</c> — <see cref="Blocked"/> is the separate field recording
/// whether the call was actually stopped. Before this split, <c>AuditOnly</c> shadow mode logged
/// every would-be deny as an Allow, producing none of the data it exists to produce.
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
    string? DecisionReason,
    bool Blocked,
    int? FilteredOutCount,
    long ElapsedMilliseconds);
