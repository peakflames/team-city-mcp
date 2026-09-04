namespace TeamCityMcpTools;

public class TeamCityClient
{
    private readonly HttpClient _httpClient;

    public TeamCityClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public HttpClient HttpClient => _httpClient;
}
