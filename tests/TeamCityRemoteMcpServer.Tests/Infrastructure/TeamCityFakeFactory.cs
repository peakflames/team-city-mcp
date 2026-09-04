namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// An <see cref="McpServerFactory"/> whose outbound TeamCity <c>HttpClient</c> is routed through a
/// <see cref="FakeTeamCityHandler"/> instead of a real server — the harness the 32 tools'
/// <c>tools/call</c> bodies had zero coverage under before this session. Installed via
/// <c>ConfigureHttpClientDefaults</c> rather than naming the typed client, per
/// <see cref="FakeTeamCityHandler"/>'s own doc comment.
/// </summary>
public sealed class TeamCityFakeFactory : McpServerFactory
{
    public FakeTeamCityHandler Handler { get; } = new();

    public TeamCityFakeFactory()
    {
        With("TEAM_CITY_URL", "https://teamcity.example.invalid");
        With("TEAM_CITY_ACCESS_TOKEN", "test-token");
        WithTestServices(services =>
            services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => Handler)));
    }
}
