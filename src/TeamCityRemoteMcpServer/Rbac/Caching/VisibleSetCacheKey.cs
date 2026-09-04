namespace TeamCityRemoteMcpServer.Rbac.Caching;

/// <summary>
/// A <c>record struct</c> for free structural equality/hashing, mirroring <see cref="PermissionCacheKey"/>.
/// The key must include the permission — a visible set is only ever computed for one permission at
/// a time, and caching across permissions would let a broad grant on one permission answer for a
/// narrower one.
/// </summary>
public readonly record struct VisibleSetCacheKey(string Identity, string Permission);
