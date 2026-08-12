using ModelContextProtocol.Protocol;

namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// The single central interception point for every <c>tools/call</c> — registered via
/// <c>services.Configure&lt;McpServerOptions&gt;(o =&gt; o.Filters.Request.CallToolFilters.Add(...))</c>.
/// Resolves the caller's identity once, defers the actual decision to <see cref="RbacGateDecider"/>,
/// emits exactly one <see cref="AccessAuditRecord"/> before ever calling <c>next</c>, and is the
/// single fail-closed short-circuit site. Threading a
/// <c>RequestContext&lt;CallToolRequestParams&gt;</c> parameter through all 32 tool signatures was
/// rejected because a forgotten signature is a silent fail-open; a missing filter registration is
/// caught once by <see cref="IRbacCallContextAccessor"/>'s null check instead.
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
            var resourceResolver = services.GetRequiredService<IResourceProjectResolver>();
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

            var argumentState = RbacGateDecider.TryExtractResource(spec.Kind, arguments, out var extractedResource);
            var resource = argumentState == RbacGateDecider.ArgumentState.Present ? extractedResource : null;

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
                var decision = await RbacGateDecider.DecideAsync(
                    gate, resourceResolver, toolName, teamCityUserId, argumentState, resource, cancellationToken);

                // Elapsed here is gate latency only — the number the RBAC budget is actually about —
                // not the downstream tool body's own latency.
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var blocked = !decision.Allowed && !options.AuditOnly;

                try
                {
                    return blocked ? DeniedResult() : await next(request, cancellationToken);
                }
                finally
                {
                    // Recorded in this inner finally, not before next(...): a G4 tool body reports
                    // FilteredOutCount mid-call via IRbacToolCallContext, writing onto the same
                    // RbacCallContext instance accessor.Current already holds — reading it back here
                    // is the only place that value is available. Still runs on a throwing tool body
                    // (proven by RbacAuditOnlyTests.ThrowingToolBody_StillEmitsItsAuditRecord), so the
                    // audit record for this call always exists regardless of how next(...) exits.
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
                        decision.Allowed ? AccessDecision.Allow : AccessDecision.Deny,
                        decision.Reason,
                        blocked,
                        accessor.Current?.FilteredOutCount,
                        elapsedMs));
                }
            }
            finally
            {
                accessor.Current = null;
            }
        };
    }

    private static CallToolResult DeniedResult() => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = ToolGate.DeniedMessage }],
    };
}
