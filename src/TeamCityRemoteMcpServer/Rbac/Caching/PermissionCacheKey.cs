namespace TeamCityRemoteMcpServer.Rbac.Caching;

/// <summary>
/// A <c>record struct</c> for free structural equality/hashing — no custom comparer needed.
/// <see cref="ProjectId"/> is null for the global (<c>global:true</c>) entry, so
/// <c>CheckProjectAsync</c>, <c>CheckProjectsAsync</c> (one entry per project id — a batch of 40 with
/// 35 already cached issues one chunk for the remaining 5), and a global check all share the one
/// permission cache instance.
/// </summary>
public readonly record struct PermissionCacheKey(string Identity, string Permission, string? ProjectId);
