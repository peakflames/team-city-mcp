namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Records how many times each <see cref="IPermissionGate"/> member was invoked — used to prove a
/// malformed/absent argument denies (or allows, for the optional-unscoped case) without ever calling
/// into the gate, which a naive "deny, but still ask the gate" implementation could pass by accident.
/// Always allows when called, so it can be composed with assertions on <see cref="ProjectCallCount"/>
/// alone.
/// </summary>
public sealed class CountingGate : IPermissionGate
{
    public int ProjectCallCount { get; private set; }

    public int ProjectsCallCount { get; private set; }

    public int GlobalCallCount { get; private set; }

    /// <summary>The exact <c>projectId</c> string passed to the most recent
    /// <see cref="CheckProjectAsync"/> call — pins that the gate receives the byte-identical,
    /// never-normalized argument value.</summary>
    public string? LastProjectId { get; private set; }

    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string toolName, string identity, string projectId, CancellationToken cancellationToken = default)
    {
        ProjectCallCount++;
        LastProjectId = projectId;
        return ValueTask.FromResult(GateDecision.Allow());
    }

    public ValueTask<GateDecision> CheckProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default)
    {
        ProjectsCallCount++;
        return ValueTask.FromResult(GateDecision.Allow());
    }

    public ValueTask<GateDecision> CheckGlobalAsync(
        string toolName, string identity, CancellationToken cancellationToken = default)
    {
        GlobalCallCount++;
        return ValueTask.FromResult(GateDecision.Allow());
    }

    public int VisibleSetCallCount { get; private set; }

    public ValueTask<VisibleProjectSet> GetVisibleProjectSetAsync(
        string toolName, string identity, CancellationToken cancellationToken = default)
    {
        VisibleSetCallCount++;
        return ValueTask.FromResult(VisibleProjectSet.Scoped(new HashSet<string>(StringComparer.Ordinal)));
    }

    public int FilterProjectsCallCount { get; private set; }

    public ValueTask<IReadOnlySet<string>> FilterProjectsAsync(
        string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default)
    {
        FilterProjectsCallCount++;
        return ValueTask.FromResult<IReadOnlySet<string>>(new HashSet<string>(projectIds, StringComparer.Ordinal));
    }
}
