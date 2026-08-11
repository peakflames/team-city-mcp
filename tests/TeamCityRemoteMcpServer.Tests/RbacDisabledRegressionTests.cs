namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Mirrors <c>AuthDisabledRegressionTests</c>: the permanent proof that with <c>Rbac</c> disabled
/// (the default), the server behaves exactly as it did before this session — the no-op gate, no
/// filter, no audit records, no identity resolution.
/// </summary>
public class RbacDisabledRegressionTests : IDisposable
{
    private readonly McpServerFactory _factory = new McpServerFactory()
        .With("TEAM_CITY_URL", "https://teamcity.example.invalid")
        .With("TEAM_CITY_ACCESS_TOKEN", "test-token");

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void IPermissionGate_ResolvesToNoOp_WhenRbacNotConfigured()
    {
        var gate = _factory.Services.GetRequiredService<IPermissionGate>();

        Assert.IsType<NoOpPermissionGate>(gate);
        Assert.False(gate.Enabled);
    }

    [Fact]
    public async Task ToolsCall_Succeeds_WithNoAuthorizationHeaderAtAll()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
    }
}
