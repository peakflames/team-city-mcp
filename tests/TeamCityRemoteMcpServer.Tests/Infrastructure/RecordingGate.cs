namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Substituted for the real gate via <see cref="McpServerFactory.WithPostAuthServices"/> (which
/// runs after <c>AddRbac</c>'s own <c>Replace()</c> call, the only seam that can win against it).
/// Every check deliberately resolves <see cref="IRbacCallContextAccessor"/> from a brand-new
/// sibling <c>IServiceScope</c> — not from any service captured by the filter — to prove the
/// <c>AsyncLocal</c> value <see cref="RbacIdentityFilter"/> sets is visible regardless of which DI
/// scope reads it, the exact property a gated tool body's own <c>CreateAsyncScope()</c> relies on.
/// </summary>
public sealed class RecordingGate : IPermissionGate
{
    private readonly IServiceProvider _rootServices;

    public List<RecordedCall> Calls { get; } = [];

    public RecordingGate(IServiceProvider rootServices)
    {
        _rootServices = rootServices;
    }

    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default)
    {
        ToolResourcePermissionMap.TryGet(toolName, out var spec);

        using var siblingScope = _rootServices.CreateScope();
        var observed = siblingScope.ServiceProvider.GetRequiredService<IRbacCallContextAccessor>().Current;

        Calls.Add(new RecordedCall(toolName, identity, projectId, spec.Permission, observed));
        return ValueTask.FromResult(GateDecision.Allow());
    }

    public ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow());

    public ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow());

    public ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
        string toolName, string identity, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(VisibleProjectSet.Global());

    public ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlySet<string>>(new HashSet<string>(projectIds, StringComparer.Ordinal));

    public sealed record RecordedCall(
        string ToolName, string Identity, string ProjectId, string? Permission, RbacCallContext? ObservedViaSiblingScope);
}
