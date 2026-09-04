namespace TeamCityMcpTools.Rbac;

/// <summary>The default for both hosts, same rationale as <see cref="NoOpPermissionGate"/>: the
/// remote host's RBAC branch <c>Replace()</c>s this registration when <c>Rbac:Enabled</c> is true.
/// Always reports no identity, so a tool body's filtering branch never triggers.</summary>
public sealed class NoOpRbacToolCallContext : IRbacToolCallContext
{
    public string? CurrentIdentity => null;

    public void ReportFilteredOut(int count)
    {
    }
}
