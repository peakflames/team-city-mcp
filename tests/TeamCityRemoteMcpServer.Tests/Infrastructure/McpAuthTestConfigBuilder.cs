using StubAuthorizationServer;

namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Starts a real stub external AS on a loopback Kestrel listener and wires an
/// <see cref="McpServerFactory"/>'s <c>McpAuth:*</c> config to point at it. Replaces the deleted
/// embedded-AS <c>McpAuthConfigBuilder</c> — that version handed tests a fixed issuer string and an
/// <c>ISigningKeyProvider</c> resolved straight out of DI, with no HTTP hop at all. Under the
/// external-AS design, <c>ConfigureJwtBearerOptions</c> fetches JWKS from <c>McpAuth:Issuer</c> over
/// real HTTP, so tests need a real (if minimal) AS listening somewhere, not just a shared key.
/// </summary>
public sealed class McpAuthTestConfigBuilder : IDisposable
{
    public const string ResourceUri = "https://teamcity-mcp.example.invalid/mcp";

    private readonly WebApplication _stubAs;

    public string Issuer { get; }

    public McpAuthTestConfigBuilder()
    {
        _stubAs = StubAuthorizationServerApp.Build([], builder => builder.WebHost.UseUrls("http://127.0.0.1:0"));
        _stubAs.Start();
        Issuer = _stubAs.Urls.First().TrimEnd('/');
    }

    /// <summary>Mints a token signed with this stub AS's own key/kid — the only combination its
    /// JWKS endpoint publishes, so this is the one signing key a token can use and still validate.</summary>
    public string CreateAccessToken(IEnumerable<string> scopes, string? email = null, string subject = "test-subject") =>
        TestTokenFactory.CreateAccessToken(SigningKey.Rsa, SigningKey.KeyId, Issuer, ResourceUri, scopes, subject, email: email);

    public McpServerFactory Apply(McpServerFactory factory) => factory
        .WithEnvironment(Environments.Development)
        .With("McpAuth:Enabled", "true")
        .With("McpAuth:Issuer", Issuer)
        .With("McpAuth:ResourceUri", ResourceUri);

    public void Dispose()
    {
        _stubAs.StopAsync().GetAwaiter().GetResult();
        _stubAs.DisposeAsync().GetAwaiter().GetResult();
    }
}
