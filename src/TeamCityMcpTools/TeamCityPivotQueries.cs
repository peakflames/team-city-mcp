using System.Text.Json.Serialization.Metadata;

namespace TeamCityMcpTools;

/// <summary>
/// Shared id -> project pivot queries against TeamCity's REST API, using the narrowest possible
/// 'fields=' selector for each pivot. Used both by 'teamcity_list_mutes' (which resolves a
/// user-facing scope, not an RBAC decision) and the remote server's RBAC pivot resolver (which
/// turns the same lookup into an allow/deny). Deliberately returns a plain outcome/data struct
/// rather than throwing — each caller applies its own error semantics: a user-facing "ERROR: ..."
/// string here, a fail-closed deny there.
/// </summary>
public static class TeamCityPivotQueries
{
    public enum PivotOutcome { Found, NotFound, UpstreamError }

    public readonly record struct BuildPivot(PivotOutcome Outcome, string? BuildTypeId, string? ProjectId, int? HttpStatusCode);

    public readonly record struct ProjectPivot(PivotOutcome Outcome, string? ProjectId, int? HttpStatusCode);

    public static async Task<BuildPivot> ResolveBuildAsync(
        TeamCityClient client, string buildId, CancellationToken cancellationToken = default)
    {
        var fields = "buildTypeId,buildType(id,projectId)";
        var url = $"app/rest/builds/id:{Uri.EscapeDataString(buildId)}?fields={Uri.EscapeDataString(fields)}";

        HttpResponseMessage response;
        try
        {
            response = await client.HttpClient.GetAsync(url, cancellationToken);
        }
        catch (Exception)
        {
            return new BuildPivot(PivotOutcome.UpstreamError, null, null, null);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new BuildPivot(PivotOutcome.NotFound, null, null, (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
            return new BuildPivot(PivotOutcome.UpstreamError, null, null, (int)response.StatusCode);

        MuteBuildLookup? lookup;
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            lookup = JsonSerializer.Deserialize(json, TeamCityJsonContext.Default.MuteBuildLookup);
        }
        catch (JsonException)
        {
            return new BuildPivot(PivotOutcome.UpstreamError, null, null, (int)response.StatusCode);
        }

        var buildTypeId = lookup?.BuildTypeId ?? lookup?.BuildType?.Id;
        var projectId = lookup?.BuildType?.ProjectId;
        return buildTypeId is null && projectId is null
            ? new BuildPivot(PivotOutcome.NotFound, null, null, (int)response.StatusCode)
            : new BuildPivot(PivotOutcome.Found, buildTypeId, projectId, (int)response.StatusCode);
    }

    public static async Task<ProjectPivot> ResolveBuildTypeProjectAsync(
        TeamCityClient client, string buildTypeId, CancellationToken cancellationToken = default)
    {
        var url = $"app/rest/buildTypes/id:{Uri.EscapeDataString(buildTypeId)}?fields={Uri.EscapeDataString("projectId")}";
        return await ResolveProjectPivotAsync(client, url, s => s?.ProjectId, TeamCityJsonContext.Default.BuildTypeSummary, cancellationToken);
    }

    public static async Task<ProjectPivot> ResolveVcsRootProjectAsync(
        TeamCityClient client, string vcsRootId, CancellationToken cancellationToken = default)
    {
        var url = $"app/rest/vcs-roots/id:{Uri.EscapeDataString(vcsRootId)}?fields={Uri.EscapeDataString("project(id)")}";
        return await ResolveProjectPivotAsync(client, url, d => d?.Project?.Id, TeamCityJsonContext.Default.VcsRootDetails, cancellationToken);
    }

    private static async Task<ProjectPivot> ResolveProjectPivotAsync<T>(
        TeamCityClient client, string url, Func<T?, string?> selectProjectId, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.HttpClient.GetAsync(url, cancellationToken);
        }
        catch (Exception)
        {
            return new ProjectPivot(PivotOutcome.UpstreamError, null, null);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new ProjectPivot(PivotOutcome.NotFound, null, (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
            return new ProjectPivot(PivotOutcome.UpstreamError, null, (int)response.StatusCode);

        T? parsed;
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            parsed = JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return new ProjectPivot(PivotOutcome.UpstreamError, null, (int)response.StatusCode);
        }

        return selectProjectId(parsed) is { Length: > 0 } projectId
            ? new ProjectPivot(PivotOutcome.Found, projectId, (int)response.StatusCode)
            : new ProjectPivot(PivotOutcome.NotFound, null, (int)response.StatusCode);
    }
}
