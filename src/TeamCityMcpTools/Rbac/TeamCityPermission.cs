namespace TeamCityMcpTools.Rbac;

/// <summary>
/// TeamCity's own permission ids, as understood by <c>GET /app/rest/users/{locator}/permissions</c>.
/// Not a re-implementation of TeamCity's permission model — these are just the string ids the gate
/// passes through to TeamCity's own already-computed answer.
/// </summary>
public static class TeamCityPermission
{
    public const string ViewProject = "view_project";

    public const string ViewBuildConfigurationSettings = "view_build_configuration_settings";

    public const string ViewFileContent = "view_file_content";

    public const string ViewBuildRuntimeData = "view_build_runtime_data";

    public const string RunBuild = "run_build";

    /// <summary>Best-effort id for the audit-log global gate — unverified against a live TeamCity
    /// instance. Locator syntax and exact permission ids must be confirmed live before Session 2
    /// enforces anything (see the tracker's Session 1 findings).</summary>
    public const string ViewAuditLog = "view_audit_log";
}
