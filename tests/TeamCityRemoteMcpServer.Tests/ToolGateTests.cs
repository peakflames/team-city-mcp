namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Unit coverage for <c>ToolGate</c>'s unenforced paths — no tool body calls into it yet this
/// session, so this is the only place its <c>BeginForProjectAsync</c> preamble-replacement shape
/// (scope ownership, client-creation failure surfacing, deny-message byte-identity) is exercised.
/// </summary>
public class ToolGateTests
{
    [Fact]
    public async Task BeginForProjectAsync_WithNoOpGate_AlwaysAllows_RegardlessOfProjectId()
    {
        var services = BuildServices(new SucceedingClientFactory());

        var result = await ToolGate.BeginForProjectAsync(
            services, new NoOpPermissionGate(), "teamcity_get_project", identity: "irrelevant", projectId: "AnyProject");

        Assert.True(result.IsSuccess);
        await result.Value.DisposeAsync();
    }

    [Fact]
    public async Task BeginForProjectAsync_ClientCreationFailure_SurfacesAsResultFail()
    {
        var services = BuildServices(new FailingClientFactory("boom"));

        var result = await ToolGate.BeginForProjectAsync(
            services, new NoOpPermissionGate(), "teamcity_get_project", identity: "irrelevant", projectId: "AnyProject");

        Assert.True(result.IsFailed);
        Assert.Equal("boom", result.Errors.First().Message);
    }

    [Fact]
    public async Task BeginForProjectAsync_DeniedGate_ReturnsNotFoundShapedMessage_NotADistinctDenyMessage()
    {
        var services = BuildServices(new SucceedingClientFactory());

        var result = await ToolGate.BeginForProjectAsync(
            services, new AlwaysDenyGate(), "teamcity_get_project", identity: "someone", projectId: "AnyProject");

        Assert.True(result.IsFailed);
        Assert.Equal(ToolGate.DeniedMessage, result.Errors.First().Message);
    }

    [Fact]
    public async Task BeginForOptionalProjectAsync_WithNullProjectId_NeverCallsTheGate()
    {
        var services = BuildServices(new SucceedingClientFactory());
        var gate = new AlwaysDenyGate();

        var result = await ToolGate.BeginForOptionalProjectAsync(
            services, gate, "teamcity_list_build_types", identity: "someone", projectId: null);

        Assert.True(result.IsSuccess, "An omitted optional project id must not trigger a per-project deny.");
        await result.Value.DisposeAsync();
    }

    [Fact]
    public async Task BeginDeferredAsync_NeverCallsAnyGateMethod()
    {
        var services = BuildServices(new SucceedingClientFactory());

        var result = await ToolGate.BeginDeferredAsync(services);

        Assert.True(result.IsSuccess);
        await result.Value.DisposeAsync();
    }

    private static IServiceProvider BuildServices(ITeamCityClientFactory clientFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(clientFactory);
        return services.BuildServiceProvider();
    }

    private sealed class SucceedingClientFactory : ITeamCityClientFactory
    {
        public Task<Result<TeamCityClient>> CreateClientAsync() =>
            Task.FromResult(Result.Ok(new TeamCityClient(new HttpClient())));
    }

    private sealed class FailingClientFactory : ITeamCityClientFactory
    {
        private readonly string _message;
        public FailingClientFactory(string message) => _message = message;

        public Task<Result<TeamCityClient>> CreateClientAsync() =>
            Task.FromResult(Result.Fail<TeamCityClient>(_message));
    }

    private sealed class AlwaysDenyGate : IPermissionGate
    {
        public bool Enabled => true;

        public ValueTask<GateDecision> CheckProjectAsync(
            string toolName, string identity, string projectId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GateDecision.Deny());

        public ValueTask<GateDecision> CheckProjectsAsync(
            string toolName, string identity, IReadOnlyCollection<string> projectIds, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GateDecision.Deny());

        public ValueTask<GateDecision> CheckGlobalAsync(
            string toolName, string identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GateDecision.Deny());

        public ValueTask<IReadOnlyCollection<string>> GetVisibleProjectsAsync(
            string toolName, string identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyCollection<string>>([]);

        public ValueTask<IReadOnlyCollection<T>> FilterAllowedProjectsAsync<T>(
            string toolName,
            string identity,
            IReadOnlyCollection<T> items,
            Func<T, string?> projectIdSelector,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyCollection<T>>([]);
    }
}
