namespace TeamCityMcpTools;

public interface ITeamCityClientFactory
{
    Task<Result<TeamCityClient>> CreateClientAsync();
}
