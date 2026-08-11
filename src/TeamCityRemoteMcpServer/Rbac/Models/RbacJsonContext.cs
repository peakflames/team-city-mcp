namespace TeamCityRemoteMcpServer.Rbac.Models;

/// <summary>
/// Source-generated, trim-safe JSON context for RBAC's own TeamCity calls — mirrors
/// <c>AuthJsonContext</c>'s rationale: every RBAC response is deserialized through this context's
/// explicit <c>JsonTypeInfo&lt;T&gt;</c> overloads, never reflection-based options. Lives under
/// Rbac/Models, not the shared <c>TeamCityJsonContext</c>, so the stdio host never gains RBAC types.
/// </summary>
[JsonSerializable(typeof(RbacUserLookupResponse))]
[JsonSerializable(typeof(RbacUserRef))]
[JsonSerializable(typeof(RbacPermissionCountResponse))]
[JsonSerializable(typeof(RbacPermissionAssignmentResponse))]
[JsonSerializable(typeof(RbacPermissionAssignmentEntry))]
[JsonSerializable(typeof(RbacProjectRef))]
public partial class RbacJsonContext : JsonSerializerContext
{
}
