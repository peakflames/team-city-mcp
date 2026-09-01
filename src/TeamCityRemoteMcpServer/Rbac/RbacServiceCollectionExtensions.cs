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

        // Where the identity *value* comes from, upstream of the TeamCity lookup above. Both
        // implementations are registered concretely and the branch is a single eager read of
        // Rbac:IdentitySource — a config value that selects a service graph, not one a request can
        // change. Rbac:IdentitySource=Claim (the default) resolves ClaimIdentitySource, which is the
        // pre-existing behavior verbatim.
        var identitySource = builder.Configuration.GetValue(
            $"{RbacOptions.SectionName}:{nameof(RbacOptions.IdentitySource)}", IdentitySource.Claim);

        if (identitySource == IdentitySource.UserInfo)
        {
            // The /userinfo call needs the caller's raw bearer token, which lives on HttpContext.Items
            // because the MCP SDK dispatches a tool call through a nested service scope.
            services.AddHttpContextAccessor();
            services.AddHttpClient(OktaUserInfoEmailSource.HttpClientName, client =>
            {
                // Short and explicit: this call is on the tool-call request path, so a slow identity
                // provider must fail fast into a denial rather than hold the caller open.
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddSingleton<OktaUserInfoEmailSource>();
            services.AddSingleton<IIdentitySource>(sp => sp.GetRequiredService<OktaUserInfoEmailSource>());
            services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<OktaUserInfoEmailSource>().Cache);
        }
        else
        {
            services.AddSingleton<IIdentitySource, ClaimIdentitySource>();
        }

        // Replace(), not Add/TryAdd — removes all registration-ordering fragility between the
        // Program.cs default no-op registration and this enabled branch.
        services.AddSingleton<TeamCityPermissionGate>();
        services.Replace(ServiceDescriptor.Singleton<IPermissionGate>(sp => sp.GetRequiredService<TeamCityPermissionGate>()));

        services.AddSingleton<TeamCityResourceProjectResolver>();
        services.AddSingleton<IResourceProjectResolver>(sp => sp.GetRequiredService<TeamCityResourceProjectResolver>());

        services.AddSingleton<IRbacCallContextAccessor, RbacCallContextAccessor>();
        services.AddSingleton<IMcpAccessAuditSink, SerilogMcpAccessAuditSink>();

        // Replace(), not Add/TryAdd — same rationale as IPermissionGate above: removes ordering
        // fragility against the default NoOpRbacToolCallContext registration in Program.cs.
        services.Replace(ServiceDescriptor.Singleton<IRbacToolCallContext, RbacToolCallContextAdapter>());

        // All RBAC caches register under the same non-generic seam so one janitor sweeps all of them.
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<TeamCityPermissionGate>().Cache);
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<TeamCityPermissionGate>().VisibleSetCache);
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<Caching.CachingIdentityResolver>().Cache);
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<TeamCityResourceProjectResolver>().BuildTypeProjectCache);
        services.AddSingleton<Caching.IEvictableCache>(sp => sp.GetRequiredService<TeamCityResourceProjectResolver>().BuildProjectCache);
        services.AddHostedService<Caching.RbacCacheJanitor>();

        services.Configure<McpServerOptions>(o => o.Filters.Request.CallToolFilters.Add(RbacIdentityFilter.Create));

        return true;
    }
}
