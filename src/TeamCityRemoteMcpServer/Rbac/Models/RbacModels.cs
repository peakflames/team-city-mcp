namespace TeamCityRemoteMcpServer.Rbac.Models;

public sealed class RbacUserLookupResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("user")]
    public List<RbacUserRef>? User { get; set; }
}

public sealed class RbacUserRef
{
    // TeamCity's REST API returns user ids as a JSON number (unlike project/buildType ids, which
    // are strings) — matches the int Id convention already used for build/change/mute/test ids
    // elsewhere in TeamCityMcpTools.Models.
    [JsonPropertyName("id")]
    public int? Id { get; set; }
}

public sealed class RbacPermissionCountResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

/// <summary>
/// Shape for <c>fields=count,permissionAssignment(project(id))</c> — required for any multi-project
/// permission check (probe 11: a bare <c>fields=count</c> is ambiguous across multiple projects, since
/// "A has 2 grants, B has 0" and "A=1, B=1" both render as the same total). A
/// <see cref="RbacPermissionAssignmentEntry"/> with a null <see cref="RbacPermissionAssignmentEntry.Project"/>
/// is a global grant (probe 8) — TeamCity does not serialize a <c>global</c> field even when
/// requested, so absence of <c>project</c> is the only discriminator.
/// </summary>
public sealed class RbacPermissionAssignmentResponse
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("permissionAssignment")]
    public List<RbacPermissionAssignmentEntry>? PermissionAssignment { get; set; }
}

public sealed class RbacPermissionAssignmentEntry
{
    [JsonPropertyName("project")]
    public RbacProjectRef? Project { get; set; }
}

public sealed class RbacProjectRef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

/// <summary>
/// The subset of an OIDC <c>/userinfo</c> response this server reads. Only two fields, deliberately:
/// anything else would be profile data we have no reason to hold, and <c>email_verified</c> is
/// non-optional here because an unverified address is an unauthenticated assertion.
/// </summary>
public sealed class OidcUserInfoResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }
}
