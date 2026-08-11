namespace TeamCityMcpTools.Rbac;

/// <summary>
/// One row of <see cref="ToolResourcePermissionMap"/>. <see cref="Permission"/> is null only for
/// <see cref="ResourceKind.Ungated"/> tools. <see cref="Enforcement"/> is a required positional
/// parameter (no default) so every row must state its stage explicitly — the compiler turns a
/// forgotten row into a build break rather than a silent <see cref="GateEnforcement.Unspecified"/>
/// (which fail-closed denies at runtime instead). <see cref="CrossProjectPermission"/> is set only
/// for G5 fan-out tools, where discovered nodes outside the tool's own named resource need their own
/// batched check.
/// </summary>
public readonly record struct ToolGateSpec(
    ResourceKind Kind, string? Permission, GateEnforcement Enforcement, string? CrossProjectPermission = null);
