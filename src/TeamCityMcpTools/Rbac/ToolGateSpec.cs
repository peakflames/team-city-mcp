namespace TeamCityMcpTools.Rbac;

/// <summary>
/// One row of <see cref="ToolResourcePermissionMap"/>. <see cref="Permission"/> is null only for
/// <see cref="ResourceKind.Ungated"/> tools. <see cref="CrossProjectPermission"/> is set only for
/// G5 fan-out tools, where discovered nodes outside the tool's own named resource need their own
/// batched check.
/// </summary>
public readonly record struct ToolGateSpec(ResourceKind Kind, string? Permission, string? CrossProjectPermission = null);
