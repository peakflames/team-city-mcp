namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// The full decision logic for a <c>tools/call</c>, extracted out of <see cref="RbacIdentityFilter"/>
/// so a 20-case argument-shape matrix is 20 microsecond unit tests instead of 20 HTTP round trips.
/// Behavior is driven entirely by the map's explicit <see cref="GateEnforcement"/> per tool — never
/// inferred from <see cref="ResourceKind"/> alone — so every branch is reachable, named in the audit
/// log via <see cref="GateDecision.Reason"/>, and a map miss (<see cref="GateEnforcement.Unspecified"/>)
/// denies rather than falling through to an unchecked allow.
///
/// Read top to bottom — there is no catch-all allow branch.
/// </summary>
internal static class RbacGateDecider
{
    internal enum ArgumentState
    {
        Absent,
        Malformed,
        Present,
    }

    /// <summary>
    /// Extracts the resource argument named by <paramref name="kind"/>'s convention
    /// (projectId/buildTypeId/buildId/vcsRootId). Exact-case ordinal lookup — the filter may be
    /// stricter than the SDK's own argument binder, never looser, so any divergence surfaces as a
    /// deny, never as an unchecked allow. Never trims or case-folds <paramref name="resource"/>: the
    /// gate must check the byte-identical string the tool body will send, or the check and the fetch
    /// are silently about different resources.
    /// </summary>
    internal static ArgumentState TryExtractResource(
        ResourceKind kind, IDictionary<string, JsonElement>? arguments, out string? resource)
    {
        resource = null;

        var argumentName = kind switch
        {
            ResourceKind.Project => "projectId",
            ResourceKind.BuildType => "buildTypeId",
            ResourceKind.Build => "buildId",
            ResourceKind.VcsRoot => "vcsRootId",
            _ => null,
        };

        if (argumentName is null || arguments is null || !arguments.TryGetValue(argumentName, out var value))
            return ArgumentState.Absent;

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return ArgumentState.Absent;

        // Any non-string shape (number, bool, array, object) is the fail-open fix: a model-driven
        // client sending {"projectId": 123} for a semantically-numeric id must never fall through to
        // an unchecked allow.
        if (value.ValueKind != JsonValueKind.String)
            return ArgumentState.Malformed;

        var raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return ArgumentState.Malformed;

        resource = raw;
        return ArgumentState.Present;
    }

    /// <summary>Per-<see cref="ResourceKind"/> audit reason for a <see cref="GateEnforcement.DeferredToLaterSession"/>
    /// tool, so grepping the audit log yields the unenforced inventory per family without touching code.</summary>
    internal static string DeferredReason(ResourceKind kind) => kind switch
    {
        ResourceKind.BuildType => "deferred_buildtype_pivot",
        ResourceKind.Build => "deferred_build_pivot",
        ResourceKind.VcsRoot => "deferred_vcsroot_pivot",
        ResourceKind.CrossProject => "deferred_visible_set",
        ResourceKind.Global => "deferred_global_permission",
        _ => "deferred",
    };

    /// <summary>
    /// The ordered contract. <paramref name="argumentState"/>/<paramref name="resource"/> are passed
    /// in already extracted (via <see cref="TryExtractResource"/>) rather than recomputed here, so
    /// the caller can populate <see cref="RbacCallContext"/> with the same resource value before this
    /// decision runs — required so a real gate call made from inside this method observes the
    /// AsyncLocal context the filter already set.
    /// </summary>
    internal static async ValueTask<GateDecision> DecideAsync(
        IPermissionGate gate,
        string toolName,
        string? identity,
        ArgumentState argumentState,
        string? resource,
        CancellationToken cancellationToken)
    {
        if (!gate.Enabled)
            return GateDecision.Allow("rbac_disabled");

        if (!ToolResourcePermissionMap.TryGet(toolName, out var spec))
            return GateDecision.Deny("tool_unmapped");

        if (spec.Enforcement == GateEnforcement.NeverGated)
            return GateDecision.Allow("never_gated");

        // One uniform rule for all 31 gated tools, deliberately checked before the Deferred branch:
        // an unresolvable caller denies even on tools this session doesn't yet enforce.
        if (identity is null)
            return GateDecision.Deny("identity_unresolved");

        if (spec.Enforcement == GateEnforcement.DeferredToLaterSession)
            return GateDecision.Allow(DeferredReason(spec.Kind));

        if (spec.Enforcement is GateEnforcement.RequiredProjectArgument or GateEnforcement.OptionalProjectArgument)
        {
            return argumentState switch
            {
                ArgumentState.Malformed => GateDecision.Deny("resource_argument_malformed"),
                ArgumentState.Absent when spec.Enforcement == GateEnforcement.OptionalProjectArgument =>
                    GateDecision.Allow("unscoped_pending_visible_set"),
                ArgumentState.Absent => GateDecision.Deny("resource_argument_missing"),
                ArgumentState.Present =>
                    await gate.CheckProjectAsync(toolName, identity, resource!, cancellationToken),
                _ => GateDecision.Deny("enforcement_unspecified"),
            };
        }

        return GateDecision.Deny("enforcement_unspecified");
    }
}
