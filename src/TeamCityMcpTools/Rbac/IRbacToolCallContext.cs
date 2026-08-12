namespace TeamCityMcpTools.Rbac;

/// <summary>
/// The narrow slice of the remote host's per-call RBAC state that a tool body in this shared
/// library is allowed to see — deliberately not the full <c>RbacCallContext</c> (which also carries
/// <c>ResourceKind</c>, <c>Permission</c>, and the raw OAuth claim value, all audit/policy concepts
/// that belong to the remote host, not to a tool body). A tool body needs exactly two things: whose
/// visible set to intersect against, and a way to report how many rows it dropped so the access
/// audit record for this call can carry that count.
///
/// The stdio host registers <see cref="NoOpRbacToolCallContext"/>, matching <see cref="NoOpPermissionGate"/>'s
/// role — no HTTP identity ever reaches that host, so <see cref="CurrentIdentity"/> is always null
/// there and filtering never triggers.
/// </summary>
public interface IRbacToolCallContext
{
    /// <summary>The calling identity's TeamCity user id, or null when RBAC is off, the host is
    /// stdio, or identity resolution failed for this call. A tool body must treat null the same way
    /// <see cref="IPermissionGate.Enabled"/> being false is treated elsewhere: skip filtering
    /// entirely rather than filter to nothing.</summary>
    string? CurrentIdentity { get; }

    /// <summary>Records how many rows this call's own filtering dropped, so the access audit record
    /// — written by the remote host after the tool body returns — can carry it. A no-op wherever
    /// there is no audit record to attach it to.</summary>
    void ReportFilteredOut(int count);
}
