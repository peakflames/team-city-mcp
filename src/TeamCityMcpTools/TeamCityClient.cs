using System.Net.Http.Headers;

namespace TeamCityMcpTools;

public class TeamCityClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;

    public TeamCityClient(string serverUrl, string accessToken)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_serverUrl + "/")
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<Result> ConnectAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("app/rest/server");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return Result.Fail("Authentication failed: invalid or missing access token.");

            if (!response.IsSuccessStatusCode)
                return Result.Fail($"TeamCity server returned {(int)response.StatusCode}: {response.ReasonPhrase}");

            return Result.Ok();
        }
        catch (HttpRequestException ex)
        {
            return Result.Fail($"Unable to reach TeamCity server at {_serverUrl}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Unexpected error connecting to TeamCity: {ex.Message}");
        }
    }

    public HttpClient HttpClient => _httpClient;
}
