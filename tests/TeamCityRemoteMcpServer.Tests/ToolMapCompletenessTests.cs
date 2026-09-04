using ModelContextProtocol.Server;

namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Reflects over every <c>[McpServerTool]</c> method on <c>BuildTools</c>/<c>ProjectTools</c> and
/// cross-checks it against <see cref="ToolResourcePermissionMap"/> and the live <c>tools/list</c>
/// response. Reflection is fine here — it is only forbidden inside
/// <see cref="ToolResourcePermissionMap"/> itself, which must build clean under
/// <c>PublishTrimmed</c>.
/// </summary>
public class ToolMapCompletenessTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public ToolMapCompletenessTests(TestServerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryMcpServerTool_HasExactlyOneMapEntry_AndNoMapEntryIsOrphaned()
    {
        var attributeNames = GetDeclaredToolNames();
        var mapNames = ToolResourcePermissionMap.Entries.Keys.ToHashSet(StringComparer.Ordinal);

        var missingFromMap = attributeNames.Except(mapNames).ToList();
        var orphanedInMap = mapNames.Except(attributeNames).ToList();

        Assert.True(missingFromMap.Count == 0, $"Tools with no ToolResourcePermissionMap entry: {string.Join(", ", missingFromMap)}");
        Assert.True(orphanedInMap.Count == 0, $"Map entries with no matching [McpServerTool]: {string.Join(", ", orphanedInMap)}");
        Assert.Equal(32, attributeNames.Count);
        Assert.Equal(32, mapNames.Count);
    }

    [Fact]
    public void EveryMcpServerTool_UsesATeamCityToolNamesConst_NeverALiteral()
    {
        var toolNameConsts = typeof(TeamCityToolNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var attributeNames = GetDeclaredToolNames();

        Assert.True(
            attributeNames.SetEquals(toolNameConsts),
            "TeamCityToolNames must declare exactly the set of [McpServerTool] names, no more, no fewer.");
    }

    [Fact]
    public async Task ToolsList_IncludesEveryMapEntry_NothingUnregisteredHides()
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
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");

        foreach (var toolName in ToolResourcePermissionMap.Entries.Keys)
        {
            Assert.Contains(toolName, body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <see cref="RbacGateDecider.TryExtractResource"/> looks up a tool's argument by exact-case
    /// ordinal name derived purely from <see cref="ResourceKind"/> — it never inspects the tool
    /// method itself. A single argument-name mismatch (a typo, a rename on one side only) would
    /// silently deny every call to that tool rather than fail loud. This walks every mapped tool
    /// whose kind names a single required argument and asserts the method actually declares a
    /// parameter with that exact name.
    /// </summary>
    [Fact]
    public void EveryResourceScopedTool_DeclaresAParameterMatchingItsResourceKind()
    {
        var methodsByName = typeof(BuildTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(ProjectTools).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(x => x.Attribute?.Name is not null)
            .ToDictionary(x => x.Attribute!.Name!, x => x.Method, StringComparer.Ordinal);

        foreach (var (toolName, spec) in ToolResourcePermissionMap.Entries)
        {
            var expectedArgumentName = spec.Kind switch
            {
                ResourceKind.Project => "projectId",
                ResourceKind.BuildType => "buildTypeId",
                ResourceKind.Build => "buildId",
                ResourceKind.VcsRoot => "vcsRootId",
                _ => null,
            };

            if (expectedArgumentName is null)
                continue;

            Assert.True(methodsByName.TryGetValue(toolName, out var method), $"{toolName} has no matching [McpServerTool] method.");

            var parameterNames = method!.GetParameters().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            Assert.True(
                parameterNames.Contains(expectedArgumentName),
                $"{toolName} is mapped as ResourceKind.{spec.Kind} (expects a '{expectedArgumentName}' argument) " +
                $"but its method declares no such parameter — RbacGateDecider.TryExtractResource would silently " +
                $"treat every call to this tool as missing the resource argument.");
        }
    }

    private static HashSet<string> GetDeclaredToolNames()
    {
        var methods = typeof(BuildTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(ProjectTools).GetMethods(BindingFlags.Public | BindingFlags.Instance));

        return methods
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Name)
            .Where(n => n is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }
}
