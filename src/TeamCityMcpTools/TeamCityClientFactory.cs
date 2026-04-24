namespace TeamCityMcpTools;

public class TeamCityClientFactory(TeamCityConfig config) : ITeamCityClientFactory
{
    private readonly TeamCityConfig _config = config;

    public async Task<Result<TeamCityClient>> CreateClientAsync()
    {
        var client = new TeamCityClient(_config.ServerUrl, _config.AccessToken);
        var result = await client.ConnectAsync();
        if (result.IsFailed)
            return Result.Fail(result.Errors.First());
        return Result.Ok(client);
    }
}
