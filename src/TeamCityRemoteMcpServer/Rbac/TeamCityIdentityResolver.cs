namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// <c>IdentityClaim</c> value -> <c>GET /app/rest/users?locator=email:{value}</c>, falling back to
/// <c>username:{value}</c>. The doc's <c>email == username</c> convention is a load-bearing tenancy
/// assumption that LDAP/AD-synced instances routinely violate, so the lookup goes through
/// <c>email:</c> first rather than assuming the claim value already *is* the username. Zero
/// matches or more than one match both resolve to null — an ambiguous match is exactly the failure
/// mode a fail-closed identity resolver must never paper over.
/// </summary>
public sealed class TeamCityIdentityResolver : IIdentityResolver
{
    private readonly ITeamCityClientFactory _clientFactory;
    private readonly ILogger<TeamCityIdentityResolver> _logger;

    public TeamCityIdentityResolver(ITeamCityClientFactory clientFactory, ILogger<TeamCityIdentityResolver> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<string?> ResolveAsync(string identityClaimValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identityClaimValue))
            return null;

        var clientResult = await _clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
        {
            _logger.LogWarning(
                "RBAC identity resolution could not create a TeamCity client: {Message}",
                clientResult.Errors.First().Message);
            return null;
        }

        var client = clientResult.Value;

        var byEmail = await TryResolveAsync(client, "email", identityClaimValue, cancellationToken);
        if (byEmail is not null)
            return byEmail;

        return await TryResolveAsync(client, "username", identityClaimValue, cancellationToken);
    }

    private async Task<string?> TryResolveAsync(
        TeamCityClient client, string locatorDimension, string value, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"app/rest/users?locator={locatorDimension}:{Uri.EscapeDataString(value)}&fields=count,user(id)";
            using var response = await client.HttpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize(json, RbacJsonContext.Default.RbacUserLookupResponse);

            if (result?.Count != 1 || result.User is not { Count: 1 } users || users[0].Id is not { } id)
                return null;

            return id.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "RBAC identity resolution failed while querying TeamCity by {LocatorDimension}.", locatorDimension);
            return null;
        }
    }
}
