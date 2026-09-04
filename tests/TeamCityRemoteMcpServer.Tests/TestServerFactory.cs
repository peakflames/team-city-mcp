using Microsoft.AspNetCore.Mvc.Testing;
using TeamCityRemoteMcpServer;

namespace TeamCityRemoteMcpServer.Tests;

public class TestServerFactory : WebApplicationFactory<Program>
{
    public TestServerFactory()
    {
        Environment.SetEnvironmentVariable("TEAM_CITY_URL", "https://teamcity.example.invalid");
        Environment.SetEnvironmentVariable("TEAM_CITY_ACCESS_TOKEN", "test-token");
    }
}
