namespace TeamCityRemoteMcpServer.Rbac;

/// <summary>
/// <c>IdentityClaim</c> value -> <c>GET /app/rest/users?locator=email:{value}</c>, falling back to
/// <c>username:{value}</c>. The doc's <c>email == username</c> convention is a load-bearing tenancy
/// assumption that LDAP/AD-synced instances routinely violate, so the lookup goes through
/// <c>email:</c> first rather than assuming the claim value already *is* the username.
///
/// Implements both <see cref="IIdentityResolver"/> (the plain <c>string?</c> shape every existing
/// caller depends on — never throws, collapses every failure to null) and <see cref="IIdentityLookup"/>
/// (the cache-facing seam, which distinguishes a definitive zero/ambiguous-match answer from a "we
/// could not ask" failure by throwing <see cref="IdentityLookupException"/> for the latter) so
/// <see cref="Caching.CachingIdentityResolver"/> can wrap this without changing this class's public
/// contract.
///
/// Takes <see cref="IServiceProvider"/>, not <see cref="ITeamCityClientFactory"/>, for the same
/// captive-dependency reason as <see cref="TeamCityPermissionGate"/>: this resolver is a singleton
/// (wrapped by the cache, which must survive across calls), and a singleton holding a transient
/// <see cref="ITeamCityClientFactory"/> would pin one <c>HttpClient</c> handler chain for the life of
/// the process. Every call opens its own <c>CreateAsyncScope()</c>.
/// </summary>
public sealed class TeamCityIdentityResolver : IIdentityResolver, IIdentityLookup
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TeamCityIdentityResolver> _logger;

    public TeamCityIdentityResolver(IServiceProvider serviceProvider, ILogger<TeamCityIdentityResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<string?> ResolveAsync(string identityClaimValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await LookupAsync(identityClaimValue, cancellationToken);
            return result.Outcome == IdentityOutcome.Resolved ? result.UserId : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RBAC identity resolution failed for '{IdentityClaimValue}'.", identityClaimValue);
            return null;
        }
    }

    async Task<IdentityResult> IIdentityLookup.LookupAsync(string claimValue, CancellationToken cancellationToken) =>
        await LookupAsync(claimValue, cancellationToken);

    private async Task<IdentityResult> LookupAsync(string claimValue, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
            return new IdentityResult(IdentityOutcome.NotFound, null);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<ITeamCityClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
        {
            _logger.LogWarning(
                "RBAC identity resolution could not create a TeamCity client: {Message}",
                clientResult.Errors.First().Message);
            throw new IdentityLookupException("client_unavailable");
        }

        var client = clientResult.Value;

        var byEmail = await LookupByDimensionAsync(client, "email", claimValue, cancellationToken);
        if (byEmail.Outcome != IdentityOutcome.NotFound)
            return byEmail;

        return await LookupByDimensionAsync(client, "username", claimValue, cancellationToken);
    }

    private async Task<IdentityResult> LookupByDimensionAsync(
        TeamCityClient client, string locatorDimension, string value, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            var url = $"app/rest/users?locator={locatorDimension}:{Uri.EscapeDataString(value)}&fields=count,user(id)";
            response = await client.HttpClient.GetAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "RBAC identity resolution transport failure querying TeamCity by {LocatorDimension}.", locatorDimension);
            throw new IdentityLookupException("upstream_transport_error");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "RBAC identity resolution got HTTP {StatusCode} querying TeamCity by {LocatorDimension}.",
                    (int)response.StatusCode, locatorDimension);
                throw new IdentityLookupException($"upstream_status_{(int)response.StatusCode}");
            }

            RbacUserLookupResponse? result;
            try
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                result = JsonSerializer.Deserialize(json, RbacJsonContext.Default.RbacUserLookupResponse);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex, "RBAC identity resolution got a malformed response querying TeamCity by {LocatorDimension}.", locatorDimension);
                throw new IdentityLookupException("upstream_malformed_response");
            }

            if (result?.Count is null)
                throw new IdentityLookupException("upstream_malformed_response");

            if (result.Count == 0)
                return new IdentityResult(IdentityOutcome.NotFound, null);

            if (result.Count > 1)
                return new IdentityResult(IdentityOutcome.Ambiguous, null);

            if (result.User is not { Count: 1 } users || users[0].Id is not { } id)
                throw new IdentityLookupException("upstream_malformed_response");

            return new IdentityResult(IdentityOutcome.Resolved, id.ToString(CultureInfo.InvariantCulture));
        }
    }
}
