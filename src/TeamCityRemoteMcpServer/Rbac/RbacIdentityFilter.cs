using ModelContextProtocol.Protocol;

namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// The single central interception point for every <c>tools/call</c> — registered via
/// <c>services.Configure&lt;McpServerOptions&gt;(o =&gt; o.Filters.Request.CallToolFilters.Add(...))</c>.
/// Resolves the caller's identity once, looks up the tool's resource kind/permission from
/// <see cref="ToolResourcePermissionMap"/>, calls into <see cref="IPermissionGate"/>, emits exactly
/// one <see cref="AccessAuditRecord"/>, and is the single fail-closed short-circuit site. Threading
/// a <c>RequestContext&lt;CallToolRequestParams&gt;</c> parameter through all 32 tool signatures was
/// rejected because a forgotten signature is a silent fail-open; a missing filter registration is
/// caught once by <see cref="IRbacCallContextAccessor"/>'s null check instead.
///
/// This session, <see cref="AlwaysAllowPermissionGate"/> never denies, so the deny branch below is
/// unenforced in practice — it exists so Session 2's real gate is a drop-in replacement, not a
/// rewrite of this filter.
/// </summary>
public static class RbacIdentityFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Create(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (request, cancellationToken) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var services = request.Services
                ?? throw new InvalidOperationException("RbacIdentityFilter requires RequestContext.Services.");

            var options = services.GetRequiredService<IOptions<RbacOptions>>().Value;
            var gate = services.GetRequiredService<IPermissionGate>();
            var identityResolver = services.GetRequiredService<IIdentityResolver>();
            var accessor = services.GetRequiredService<IRbacCallContextAccessor>();
            var auditSink = services.GetRequiredService<IMcpAccessAuditSink>();

            var toolName = request.Params?.Name ?? string.Empty;
            var arguments = request.Params?.Arguments;

            ToolResourcePermissionMap.TryGet(toolName, out var spec);

            var identityClaimValue = request.User?.FindFirst(options.IdentityClaim)?.Value;
            var teamCityUserId = identityClaimValue is not null
                ? await identityResolver.ResolveAsync(identityClaimValue, cancellationToken)
                : null;

            var resource = ExtractResource(spec.Kind, arguments);

            accessor.Current = new RbacCallContext
            {
                ToolName = toolName,
                ResourceKind = spec.Kind,
                Permission = spec.Permission,
                Resource = resource,
                IdentityClaimValue = identityClaimValue,
                TeamCityUserId = teamCityUserId,
            };

            try
            {
                var decision = await DecideAsync(gate, spec, teamCityUserId, resource, toolName, cancellationToken);
                var allowed = decision.Allowed || options.AuditOnly;

                var result = allowed
                    ? await next(request, cancellationToken)
                    : DeniedResult();

                auditSink.Record(new AccessAuditRecord(
                    DateTimeOffset.UtcNow,
                    request.User?.FindFirst("sub")?.Value,
                    request.User?.FindFirst("client_id")?.Value,
                    request.User?.FindFirst("jti")?.Value,
                    identityClaimValue,
                    teamCityUserId,
                    toolName,
                    resource,
                    spec.Permission,
                    allowed ? AccessDecision.Allow : AccessDecision.Deny,
                    null,
                    stopwatch.ElapsedMilliseconds));

                return result;
            }
            finally
            {
                accessor.Current = null;
            }
        };
    }

    private static async ValueTask<GateDecision> DecideAsync(
        IPermissionGate gate,
        ToolGateSpec spec,
        string? identity,
        string? resource,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (!gate.Enabled || spec.Kind == ResourceKind.Ungated)
            return GateDecision.Allow();

        if (identity is null)
            return GateDecision.Deny("identity_unresolved");

        return spec.Kind switch
        {
            ResourceKind.Project or ResourceKind.BuildType or ResourceKind.Build or ResourceKind.VcsRoot
                when resource is not null =>
                await gate.CheckProjectAsync(toolName, identity, resource, cancellationToken),
            ResourceKind.Global =>
                await gate.CheckGlobalAsync(toolName, identity, cancellationToken),
            // CrossProject tools, and Project/BuildType/Build/VcsRoot tools whose optional resource
            // argument was omitted, are decided by the tool body's own visible-set filtering (once
            // it exists, in a later session) — not by this pre-check.
            _ => GateDecision.Allow(),
        };
    }

    private static string? ExtractResource(ResourceKind kind, IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null)
            return null;

        var argumentName = kind switch
        {
            ResourceKind.Project => "projectId",
            ResourceKind.BuildType => "buildTypeId",
            ResourceKind.Build => "buildId",
            ResourceKind.VcsRoot => "vcsRootId",
            _ => null,
        };

        if (argumentName is null)
            return null;

        return arguments.TryGetValue(argumentName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static CallToolResult DeniedResult() => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = ToolGate.DeniedMessage }],
    };
}
