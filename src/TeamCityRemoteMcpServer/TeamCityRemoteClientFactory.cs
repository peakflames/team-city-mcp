using TeamCityMcpTools;
using FluentResults;

namespace TeamCityRemoteMcpServer;

public class TeamCityRemoteClientFactory : ITeamCityClientFactory
{
    private readonly TeamCityConfig _config;
    private readonly ILogger<TeamCityRemoteClientFactory> _logger;

    public TeamCityRemoteClientFactory(TeamCityConfig config, ILogger<TeamCityRemoteClientFactory> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<Result<TeamCityClient>> CreateClientAsync()
    {
        _logger.LogDebug("Creating TeamCity client for server: {ServerUrl}", _config.ServerUrl);

        var client = new TeamCityClient(_config.ServerUrl, _config.AccessToken);
        var result = await client.ConnectAsync();

        if (result.IsFailed)
        {
            var errorMessage = result.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            _logger.LogError("Failed to create TeamCity client: {ErrorMessage}", errorMessage);
            return Result.Fail($"Failed to create TeamCity client: {errorMessage}");
        }

        _logger.LogDebug("Successfully created TeamCity client for server: {ServerUrl}", _config.ServerUrl);
        return Result.Ok(client);
    }
}
