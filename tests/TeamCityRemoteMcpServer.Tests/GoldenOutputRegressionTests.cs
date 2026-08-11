namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Captures the full <c>tools/call</c> body for one tool with both McpAuth and Rbac off, and
/// asserts string equality against the exact markdown <c>ProjectTools.GetProject</c> produced
/// before this session — proof that <c>Rbac:Enabled=false</c> (today's default) is byte-identical
/// to pre-Phase-2 behavior, not just "still returns 200".
/// </summary>
public class GoldenOutputRegressionTests : IDisposable
{
    private readonly TeamCityFakeFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetProject_Output_IsByteIdentical_WithRbacAndAuthDisabled()
    {
        _factory.Handler.OnProject(
            "MyProject",
            """{"id":"MyProject","name":"My Project","description":"A test project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""");

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "tools/call",
                  "params": { "name": "teamcity_get_project", "arguments": { "projectId": "MyProject" } }
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

        var text = ExtractToolResultText(body);
        Assert.Equal(BuildExpectedMarkdown(), text);
    }

    private static string BuildExpectedMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Project: My Project");
        sb.AppendLine();
        sb.AppendLine("## Details");
        sb.AppendLine();
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|-------|-------|");
        sb.AppendLine("| ID | MyProject |");
        sb.AppendLine("| Name | My Project |");
        sb.AppendLine("| Description | A test project |");
        sb.AppendLine("| Parent Project | — (—) |");
        sb.AppendLine();
        sb.AppendLine("## Build Templates");
        sb.AppendLine();
        sb.AppendLine("None.");
        sb.AppendLine();
        return sb.ToString();
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
