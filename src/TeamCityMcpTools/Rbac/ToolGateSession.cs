namespace TeamCityMcpTools.Rbac;

/// <summary>
/// Owns the DI scope a gated tool body needs for its <see cref="TeamCityClient"/>, exactly as the
/// 5-line preamble (<c>CreateAsyncScope()</c> -> <c>ITeamCityClientFactory</c> ->
/// <c>CreateClientAsync()</c>) does today. Disposing this disposes the scope. Not yet used by any
/// tool body this session — <see cref="ToolGate"/> ships with only its unenforced paths exercised,
/// by unit test.
/// </summary>
public sealed class ToolGateSession : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;

    public TeamCityClient Client { get; }

    internal ToolGateSession(AsyncServiceScope scope, TeamCityClient client)
    {
        _scope = scope;
        Client = client;
    }

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
