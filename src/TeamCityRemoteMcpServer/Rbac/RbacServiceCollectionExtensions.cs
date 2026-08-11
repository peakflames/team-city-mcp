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

        // TryAdd, not Add: a test's FakeTimeProvider registration must keep winning — the same
        // footgun AuthenticationServiceCollectionExtensions already documents.
        services.TryAddSingleton(TimeProvider.System);

        // TeamCityIdentityResolver <- IIdentityLookup, wrapped by the caching decorator, which is
        // what everything else (including RbacIdentityFilter) actually depends on as IIdentityResolver.
        services.AddSingleton<TeamCityIdentityResolver>();
        services.AddSingleton<IIdentityLookup>(sp => sp.GetRequiredService<TeamCityIdentityResolver>());
        services.AddSingleton<Caching.CachingIdentityResolver>();
        services.AddSingleton<IIdentityResolver>(sp => sp.GetRequiredService<Caching.CachingIdentityResolver>());

        // Replace(), not Add/TryAdd — removes all registration-ordering fragility between the
        // Program.cs default no-op registration and this enabled branch.
        services.AddSingleton<TeamCityPermissionGate>();
        services.Replace(ServiceDescriptor.Singleton<IPermissionGate>(sp => sp.GetRequiredService<TeamCityPermissionGate>()));

        services.AddSingleton<IRbacCallContextAccessor, RbacCallContextAccessor>();
        services.AddSingleton<IMcpAccessAuditSink, SerilogMcpAccessAuditSink>();

        // Both RBAC caches register under the same non-generic seam so one janitor sweeps both.
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<TeamCityPermissionGate>().Cache);
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<Caching.CachingIdentityResolver>().Cache);
        services.AddHostedService<Caching.RbacCacheJanitor>();

        services.Configure<McpServerOptions>(o => o.Filters.Request.CallToolFilters.Add(RbacIdentityFilter.Create));

        return true;
    }
}
