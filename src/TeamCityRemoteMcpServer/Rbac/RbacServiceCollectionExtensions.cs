namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// Deliberate copy of the shape of <c>AuthenticationServiceCollectionExtensions.AddMcpAuth</c>:
/// reads <c>Rbac:Enabled</c> eagerly and returns false before registering anything — no options
/// bind, no <c>ValidateOnStart</c>, no filter. Same rationale as Phase 1's comment there:
/// unconditional <c>ValidateOnStart</c> would let a malformed <c>Rbac</c> section break servers
/// with RBAC off.
/// </summary>
public static class RbacServiceCollectionExtensions
{
    public static bool AddRbac(this WebApplicationBuilder builder, IMcpServerBuilder mcpBuilder)
    {
        var enabled = builder.Configuration.GetValue($"{RbacOptions.SectionName}:Enabled", false);
        if (!enabled)
            return false;

        var services = builder.Services;

        services.AddOptions<RbacOptions>()
            .Bind(builder.Configuration.GetSection(RbacOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RbacOptions>, RbacOptionsValidator>();

        // Replace(), not Add/TryAdd — removes all registration-ordering fragility between the
        // Program.cs default no-op registration and this enabled branch.
        services.Replace(ServiceDescriptor.Singleton<IPermissionGate, AlwaysAllowPermissionGate>());
        services.AddSingleton<IIdentityResolver, TeamCityIdentityResolver>();
        services.AddSingleton<IRbacCallContextAccessor, RbacCallContextAccessor>();
        services.AddSingleton<IMcpAccessAuditSink, SerilogMcpAccessAuditSink>();

        services.Configure<McpServerOptions>(o => o.Filters.Request.CallToolFilters.Add(RbacIdentityFilter.Create));

        return true;
    }
}
