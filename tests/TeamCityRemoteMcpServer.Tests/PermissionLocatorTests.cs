namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Golden locator strings for <see cref="TeamCityPermissionGate"/>, pinned against a live probe of
/// TeamCity's permissions endpoint: <c>permission:{perm}</c> is bare —
/// <c>permission:(id:view_project)</c> is an HTTP 400 on the real server. Pure unit tests, no HTTP.
/// </summary>
public class PermissionLocatorTests
{
    [Fact]
    public void SingleProjectLocator_IsBarePermissionSyntax()
    {
        var locator = TeamCityPermissionGate.BuildSingleProjectLocator(TeamCityPermission.ViewProject, "MyProject");

        Assert.Equal("permission:view_project,project:(id:MyProject)", locator);
    }

    [Fact]
    public void GlobalLocator_UsesGlobalTrue_NeverAnUnscopedQuery()
    {
        var locator = TeamCityPermissionGate.BuildGlobalLocator(TeamCityPermission.ViewAuditLog);

        Assert.Equal("permission:view_audit_log,global:true", locator);
    }

    [Fact]
    public void BatchLocator_WrapsIdsInDoubleParens()
    {
        var locator = TeamCityPermissionGate.BuildBatchLocator(TeamCityPermission.ViewProject, ["A", "B", "C"]);

        Assert.Equal("permission:view_project,project:(id:(A,B,C))", locator);
    }

    [Theory]
    [InlineData("view_project", "MyProject")]
    [InlineData("view_audit_log", "Other.Project-1")]
    [InlineData("view_file_content", "A")]
    public void NoLocatorShape_EverUsesThePermissionIdSyntax_ThatIs400OnTheRealServer(string permission, string projectId)
    {
        // Probe 1: permission:(id:view_project) -> HTTP 400 "Unsupported value 'id:view_project'".
        // Every locator this gate builds must avoid that shape entirely.
        var single = TeamCityPermissionGate.BuildSingleProjectLocator(permission, projectId);
        var global = TeamCityPermissionGate.BuildGlobalLocator(permission);
        var batch = TeamCityPermissionGate.BuildBatchLocator(permission, [projectId, "Other"]);

        Assert.DoesNotContain("permission:(id:", single, StringComparison.Ordinal);
        Assert.DoesNotContain("permission:(id:", global, StringComparison.Ordinal);
        Assert.DoesNotContain("permission:(id:", batch, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionsUrl_ForASingleProjectCheck_UsesBareCountFields()
    {
        var url = TeamCityPermissionGate.BuildPermissionsUrl("42", "permission:view_project,project:(id:P)", multiProject: false);

        Assert.Contains("fields=count", url, StringComparison.Ordinal);
        Assert.DoesNotContain("permissionAssignment", url, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionsUrl_ForAnyMultiProjectCheck_AlwaysCarriesPermissionAssignmentProjectId()
    {
        var url = TeamCityPermissionGate.BuildPermissionsUrl(
            "42", TeamCityPermissionGate.BuildBatchLocator(TeamCityPermission.ViewProject, ["A", "B"]), multiProject: true);

        Assert.Contains("fields=count,permissionAssignment(project(id))", url, StringComparison.Ordinal);
    }

    [Fact]
    public void ChunkProjectIds_NeverEmitsALocatorOverTheCharacterBudget()
    {
        // Real-world project ids can run 50+ characters — use a realistic worst case, not short
        // synthetic ids, so the budget is actually exercised.
        var ids = Enumerable.Range(0, 500).Select(i => new string('P', 52) + i.ToString(CultureInfo.InvariantCulture)).ToArray();

        var chunks = TeamCityPermissionGate.ChunkProjectIds(ids).ToList();

        Assert.NotEmpty(chunks);
        Assert.Equal(ids, chunks.SelectMany(c => c)); // every id preserved, in order, exactly once

        foreach (var chunk in chunks)
        {
            var locator = TeamCityPermissionGate.BuildBatchLocator(TeamCityPermission.ViewProject, chunk);
            Assert.True(
                locator.Length <= TeamCityPermissionGate.BatchCharBudget,
                $"Chunk locator is {locator.Length} chars, over the {TeamCityPermissionGate.BatchCharBudget}-char budget.");
            Assert.True(chunk.Count <= TeamCityPermissionGate.BatchMaxCount);
        }
    }

    [Fact]
    public void ChunkProjectIds_SingleOversizedIdStillProducesAChunk_RatherThanLoopingForever()
    {
        // A pathological single id longer than the whole budget must still terminate in one chunk,
        // not spin — ChunkProjectIds always accepts the first id into an empty current chunk.
        var hugeId = new string('X', 5000);

        var chunks = TeamCityPermissionGate.ChunkProjectIds([hugeId]).ToList();

        var chunk = Assert.Single(chunks);
        Assert.Equal(hugeId, Assert.Single(chunk));
    }

    [Fact]
    public void ChunkProjectIds_Empty_ProducesNoChunks()
    {
        var chunks = TeamCityPermissionGate.ChunkProjectIds([]).ToList();

        Assert.Empty(chunks);
    }
}
