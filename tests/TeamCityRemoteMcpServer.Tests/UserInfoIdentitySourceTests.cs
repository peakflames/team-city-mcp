namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Covers <c>Rbac:IdentitySource=UserInfo</c> — identity resolved by calling the authorization
/// server's OIDC <c>/userinfo</c> endpoint with the caller's own token, for an authorization server
/// that puts no <c>email</c> claim on an access token.
///
/// Every case asserts on the emitted <see cref="AccessAuditRecord"/> rather than on the HTTP status,
/// because the interesting distinctions (a throttled identity provider versus a caller who has no
/// identity) are invisible in the response by design: both are the same denial to the caller.
/// </summary>
public class UserInfoIdentitySourceTests : IDisposable
{
    private const string Email = "stub.user@example.invalid";
    private const string TeamCityUserId = "42";
    private const string ProjectId = "MyProject";
    private const string ProjectJson =
        """{"id":"MyProject","name":"My Project","description":"A test project","projects":{"project":[]},"buildTypes":{"buildType":[]},"templates":{"buildType":[]}}""";

    private readonly TeamCityFakeFactory _factory;
    private readonly McpAuthTestConfigBuilder _mcpAuth = new();
    private readonly CapturingAuditSink _audit = new();
    private readonly CountingGate _gate = new();

    public UserInfoIdentitySourceTests()
    {
        _factory = new TeamCityFakeFactory();
        _mcpAuth.Apply(_factory);
        _factory.WithRbacUserInfoIdentity();
        _factory.WithPostAuthServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IMcpAccessAuditSink>(_audit));

            // An always-allowing gate keeps every case here about identity resolution. With the real
            // gate the fake TeamCity has no permission route, so every call would deny for a reason
            // that has nothing to do with what these tests measure.
            services.Replace(ServiceDescriptor.Singleton<IPermissionGate>(_gate));

            // TeamCityFakeFactory routes every HttpClient through FakeTeamCityHandler via
            // ConfigureHttpClientDefaults, which would swallow the /userinfo call too. Named-client
            // configuration applied after the defaults wins, so this puts a real socket handler back
            // on just this one client — the stub AS is a real Kestrel listener and must be reached
            // over real HTTP for the URL construction to be under test at all.
            services.AddHttpClient(OktaUserInfoEmailSource.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        _mcpAuth.Dispose();
    }

    // ---------------------------------------------------------------- happy path

    [Fact]
    public async Task ResolvesIdentityFromUserInfo_WhenTokenCarriesNoEmailClaim()
    {
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(ProjectId, ProjectJson);

        // No `email` argument: the token deliberately carries no email claim at all, which is the
        // whole point — under IdentitySource=Claim this call would deny.
        var response = await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read]));

        Assert.True(response.IsSuccessStatusCode);

        var record = Assert.Single(_audit.Records);
        Assert.Equal(Email, record.IdentityClaimValue);
        Assert.Equal(TeamCityUserId, record.TeamCityUserId);
        Assert.Equal(AccessDecision.Allow, record.Decision);
        Assert.False(record.Blocked);
        Assert.Equal(1, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task NormalizesEmailCase_BeforeTheTeamCityLookup()
    {
        _mcpAuth.State.UserInfoEmail = "Stub.User@Example.Invalid";

        // The fake only answers the lowercase locator, so a pass here proves normalization happened
        // before the lookup rather than after.
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(ProjectId, ProjectJson);

        var response = await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read]));

        Assert.True(response.IsSuccessStatusCode);
        var record = Assert.Single(_audit.Records);
        Assert.Equal(Email, record.IdentityClaimValue);
        Assert.Equal(TeamCityUserId, record.TeamCityUserId);
    }

    // ---------------------------------------------------------------- guardrails

    [Fact]
    public async Task UnverifiedEmail_Denies_WithItsOwnReason()
    {
        _mcpAuth.State.UserInfoEmailVerified = false;
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);

        await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read]));

        var record = Assert.Single(_audit.Records);
        Assert.Null(record.IdentityClaimValue);
        Assert.Equal(AccessDecision.Deny, record.Decision);
        Assert.Equal("identity_email_unverified", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    [Fact]
    public async Task PlusAddressedEmail_Denies_RatherThanNormalizingToTheBaseAddress()
    {
        _mcpAuth.State.UserInfoEmail = "stub.user+teamcity@example.invalid";

        // Deliberately registered so the test would PASS the lookup if the code stripped the tag —
        // the assertion is that it does not even try.
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);

        await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read]));

        var record = Assert.Single(_audit.Records);
        Assert.Null(record.IdentityClaimValue);
        Assert.Equal("identity_email_rejected_form", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    [Fact]
    public async Task NoEmailAtTheIdentityProvider_Denies_WithTheGenericUnresolvedReason()
    {
        _mcpAuth.State.UserInfoEmail = null;

        await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read]));

        var record = Assert.Single(_audit.Records);
        Assert.Null(record.IdentityClaimValue);
        Assert.Equal("identity_unresolved", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    // ---------------------------------------------------------------- 429 handling

    [Fact]
    public async Task RateLimited_Denies_WithADistinctReason_AndIsNotCached()
    {
        _mcpAuth.State.UserInfoRateLimited = true;

        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read]);

        await CallGetProjectAsync(token);
        await CallGetProjectAsync(token);

        Assert.Equal(2, _audit.Records.Count);
        foreach (var record in _audit.Records)
        {
            Assert.Equal("identity_userinfo_rate_limited", record.DecisionReason);
            Assert.True(record.Blocked);
        }

        // The point of the test: a 429 must not be cached, even negatively. Two calls on the same
        // token therefore produce two upstream attempts, not one cached denial reused.
        Assert.Equal(2, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task RateLimitClearing_RecoversOnTheVeryNextCall()
    {
        _mcpAuth.State.UserInfoRateLimited = true;
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(ProjectId, ProjectJson);

        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read]);
        await CallGetProjectAsync(token);

        _mcpAuth.State.UserInfoRateLimited = false;
        var response = await CallGetProjectAsync(token);

        // Would fail if the 429 had been cached: the second call would still be serving the denial.
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, _audit.Records.Count);
        Assert.Equal("identity_userinfo_rate_limited", _audit.Records[0].DecisionReason);
        Assert.Equal(Email, _audit.Records[1].IdentityClaimValue);
    }

    [Fact]
    public async Task UpstreamServerError_Denies_WithTheUnavailableReason_AndIsNotCached()
    {
        _mcpAuth.State.UserInfoStatusCode = StatusCodes.Status503ServiceUnavailable;

        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read]);
        await CallGetProjectAsync(token);
        await CallGetProjectAsync(token);

        Assert.All(_audit.Records, r => Assert.Equal("identity_userinfo_unavailable", r.DecisionReason));
        Assert.Equal(2, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task MalformedUserInfoResponse_Denies_WithTheUnavailableReason()
    {
        _mcpAuth.State.UserInfoMalformed = true;

        await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read]));

        var record = Assert.Single(_audit.Records);
        Assert.Equal("identity_userinfo_unavailable", record.DecisionReason);
    }

    // ---------------------------------------------------------------- caching

    [Fact]
    public async Task ABurstOfCallsOnOneToken_CostsExactlyOneUserInfoRequest()
    {
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(ProjectId, ProjectJson);

        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read]);

        for (var i = 0; i < 8; i++)
        {
            var response = await CallGetProjectAsync(token);
            Assert.True(response.IsSuccessStatusCode, $"call {i} failed");
        }

        Assert.Equal(8, _audit.Records.Count);
        Assert.Equal(1, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task ADifferentToken_CostsOneMoreUserInfoRequest()
    {
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(ProjectId, ProjectJson);

        // Distinct subjects so the two tokens differ byte-wise, which is what the SHA-256 cache key
        // is computed over. This is the section 4.2 volume model: one /userinfo call per fresh token.
        await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read], subject: "subject-one"));
        await CallGetProjectAsync(_mcpAuth.CreateAccessToken([OAuthScopes.Read], subject: "subject-two"));

        Assert.Equal(2, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task ConcurrentCallsOnOneToken_ShareASingleUserInfoRequest()
    {
        _factory.Handler.OnUsers("email", Email, TeamCityUserId);
        _factory.Handler.OnProject(ProjectId, ProjectJson);

        var token = _mcpAuth.CreateAccessToken([OAuthScopes.Read]);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CallGetProjectAsync(token)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));

        // TtlCache's single-flight coalescing, exercised for real: a cache that merely stored results
        // would issue six requests here.
        Assert.Equal(1, _mcpAuth.State.UserInfoRequestCount);
    }

    // ----------------------------------------------------------------

    private async Task<HttpResponseMessage> CallGetProjectAsync(string bearerToken)
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $$"""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "tools/call",
                  "params": { "name": "teamcity_get_project", "arguments": { "projectId": "{{ProjectId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return await client.SendAsync(request);
    }
}
