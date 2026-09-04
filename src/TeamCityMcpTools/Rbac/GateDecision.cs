namespace TeamCityMcpTools.Rbac;

/// <summary>Result of a permission check. <see cref="Reason"/> is for the MCP-side audit log only —
/// never surfaced to the caller, per the locked "deny is byte-identical to not-found" decision.</summary>
public readonly record struct GateDecision(bool Allowed, string? Reason = null)
{
    public static GateDecision Allow(string? reason = null) => new(true, reason);

    public static GateDecision Deny(string? reason = null) => new(false, reason);
}
