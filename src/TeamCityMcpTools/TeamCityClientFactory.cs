namespace TeamCityMcpTools;

public class TeamCityClientFactory(HttpClient httpClient) : ITeamCityClientFactory
{
    private readonly HttpClient _httpClient = httpClient;

    public Task<Result<TeamCityClient>> CreateClientAsync()
    {
        return Task.FromResult(Result.Ok(new TeamCityClient(_httpClient)));
    }
}
