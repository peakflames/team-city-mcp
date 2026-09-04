namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// The remote host's implementation of the shared library's narrow <see cref="IRbacToolCallContext"/>
/// seam — a thin read/write wrapper over the same <see cref="IRbacCallContextAccessor"/>
/// <see cref="RbacIdentityFilter"/> already populates per call. Registered only when
/// <c>Rbac:Enabled</c> is true, replacing the default <c>NoOpRbacToolCallContext</c>.
/// </summary>
public sealed class RbacToolCallContextAdapter : IRbacToolCallContext
{
    private readonly IRbacCallContextAccessor _accessor;

    public RbacToolCallContextAdapter(IRbacCallContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? CurrentIdentity => _accessor.Current?.TeamCityUserId;

    public void ReportFilteredOut(int count)
    {
        if (_accessor.Current is { } current)
        {
            current.FilteredOutCount = count;
        }
    }
}
