using TeamCityMcpTools;
using FluentResults;

namespace TeamCityRemoteMcpServer;

public class TeamCityRemoteClientFactory : ITeamCityClientFactory
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamCityRemoteClientFactory> _logger;

    public TeamCityRemoteClientFactory(HttpClient httpClient, ILogger<TeamCityRemoteClientFactory> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<Result<TeamCityClient>> CreateClientAsync()
    {
        _logger.LogDebug("Creating TeamCity client for server: {ServerUrl}", _httpClient.BaseAddress);
        return Task.FromResult(Result.Ok(new TeamCityClient(_httpClient)));
    }
}
