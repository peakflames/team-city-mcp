using Microsoft.AspNetCore.Mvc.Testing;

namespace TeamCityRemoteMcpServer.Tests.Infrastructure;

/// <summary>A resource-server test host with McpAuth:Enabled=true, pointed at a caller-supplied
/// external authorization server (normally a StubAuthorizationServerFixture).
///
/// Config is injected via environment variables, not WebHostBuilder.ConfigureAppConfiguration —
/// Program.BuildApp reads McpAuth:Enabled eagerly off WebApplicationBuilder.Configuration before
/// WebApplicationFactory's minimal-hosting interception has a chance to merge in
/// ConfigureAppConfiguration overrides, so that path is invisible to the eager read. Environment
/// variables are already a configuration source at the moment WebApplication.CreateBuilder(args)
/// runs, so they are visible immediately — the same reason TestServerFactory sets TEAM_CITY_URL
/// this way rather than through config.</summary>
public sealed class AuthEnabledTestServerFactory : WebApplicationFactory<Program>
{
    public AuthEnabledTestServerFactory(string issuer, string resourceUri)
    {
        Environment.SetEnvironmentVariable("TEAM_CITY_URL", "https://teamcity.example.invalid");
        Environment.SetEnvironmentVariable("TEAM_CITY_ACCESS_TOKEN", "test-token");

        // Validator only allows a plaintext http Issuer in Development — the stub AS listens on
        // http://127.0.0.1 for test simplicity.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        Environment.SetEnvironmentVariable("McpAuth__Enabled", "true");
        Environment.SetEnvironmentVariable("McpAuth__Issuer", issuer);
        Environment.SetEnvironmentVariable("McpAuth__ResourceUri", resourceUri);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // These three (unlike TEAM_CITY_URL/TEAM_CITY_ACCESS_TOKEN, which every test host sets
        // identically) change behavior for any *other* test class sharing this process, so they
        // must not leak past this fixture's lifetime.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("McpAuth__Enabled", null);
        Environment.SetEnvironmentVariable("McpAuth__Issuer", null);
        Environment.SetEnvironmentVariable("McpAuth__ResourceUri", null);
    }
}
