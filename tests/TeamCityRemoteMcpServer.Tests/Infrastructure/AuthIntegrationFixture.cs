using Xunit;

namespace TeamCityRemoteMcpServer.Tests.Infrastructure;

/// <summary>Composes the stub external AS with an McpAuth-enabled resource server pointed at it —
/// the harness shared by the challenge/discovery and token-validation test classes.</summary>
public sealed class AuthIntegrationFixture : IAsyncLifetime
{
    public const string ResourceUri = "https://teamcity-mcp.example.invalid/mcp";

    public StubAuthorizationServerFixture StubAs { get; } = new();

    private AuthEnabledTestServerFactory? _factory;

    public AuthEnabledTestServerFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await StubAs.InitializeAsync();
        StubAs.State.DefaultAudience = ResourceUri;
        _factory = new AuthEnabledTestServerFactory(StubAs.IssuerUrl, ResourceUri);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await StubAs.DisposeAsync();
    }
}
