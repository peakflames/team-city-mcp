namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Regression tests for issue #3: <c>teamcity_get_build_type</c> always reported "No agent
/// requirements configured." TeamCity names the fields <c>agent-requirements</c> /
/// <c>agent-requirement</c> (hyphenated) and silently ignores unknown field names, so both the
/// <c>fields</c> query and the model's JSON names have to use the hyphenated form.
/// </summary>
public class GetBuildTypeAgentRequirementsTests : IDisposable
{
    private readonly TeamCityFakeFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task OwnRequirement_IsListed_WithTypeAndProperties_AndNoInheritedMark()
    {
        var text = await CallGetBuildType(
            """
            {"id":"Proj_Build","name":"Build","projectId":"Proj","projectName":"Proj",
             "agent-requirements":{"count":1,"agent-requirement":[
               {"id":"RQ_1","type":"equals","properties":{"property":[
                 {"name":"property-name","value":"teamcity.agent.jvm.os.name"},
                 {"name":"property-value","value":"Windows 11"}]}}]}}
            """);

        Assert.Contains("### RQ_1", text);
        Assert.DoesNotContain("### RQ_1 *(inherited)*", text);
        Assert.Contains("- **Type**: equals", text);
        Assert.Contains("| property-name | teamcity.agent.jvm.os.name |", text);
        Assert.Contains("| property-value | Windows 11 |", text);
        Assert.DoesNotContain("No agent requirements configured.", text);
    }

    [Fact]
    public async Task InheritedRequirement_IsMarkedInherited()
    {
        var text = await CallGetBuildType(
            """
            {"id":"Proj_Build","name":"Build","projectId":"Proj","projectName":"Proj",
             "agent-requirements":{"count":1,"agent-requirement":[
               {"id":"RQ_2","type":"exists","inherited":true,"properties":{"property":[
                 {"name":"property-name","value":"env.DOTNET_ROOT"}]}}]}}
            """);

        Assert.Contains("### RQ_2 *(inherited)*", text);
    }

    [Fact]
    public async Task NoRequirements_ReportsNoneConfigured()
    {
        var text = await CallGetBuildType(
            """{"id":"Proj_Build","name":"Build","projectId":"Proj","projectName":"Proj"}""");

        Assert.Contains("No agent requirements configured.", text);
    }

    private async Task<string> CallGetBuildType(string buildTypeJson)
    {
        _factory.Handler.OnBuildTypeDetails("Proj_Build", buildTypeJson);

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "tools/call",
                  "params": { "name": "teamcity_get_build_type", "arguments": { "buildTypeId": "Proj_Build" } }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");

        return ExtractToolResultText(body);
    }

    private static string ExtractToolResultText(string body)
    {
        var dataLine = body
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal));

        var json = dataLine is null ? body : dataLine["data:".Length..].Trim();

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
    }
}
