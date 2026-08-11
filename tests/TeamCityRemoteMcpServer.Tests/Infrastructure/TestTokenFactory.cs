namespace TeamCityRemoteMcpServer.Tests;

/// <summary>Mints JWTs directly against a test key, bypassing the (not-yet-built) /oauth/token
/// endpoint entirely — Session 1 only needs a validated caller identity reaching /mcp.</summary>
public static class TestTokenFactory
{
    public static string CreateAccessToken(
        RSA signingKey,
        string kid,
        string issuer,
        string audience,
        IEnumerable<string> scopes,
        string subject = "test-subject",
        DateTime? notBefore = null,
        DateTime? expires = null,
        string? email = null)
    {
        var securityKey = new RsaSecurityKey(signingKey) { KeyId = kid };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        return CreateToken(issuer, audience, scopes, subject, notBefore, expires, credentials, email);
    }

    /// <summary>Signs with HMAC using the RSA key's own public modulus as the HMAC secret — the
    /// classic RS256/HS256 algorithm-confusion attack. Must be rejected even though the `kid`
    /// matches a real signing key, because ValidAlgorithms pins the resource server to RS256.</summary>
    public static string CreateHmacConfusionToken(
        RSA signingKey,
        string kid,
        string issuer,
        string audience,
        IEnumerable<string> scopes)
    {
        var modulus = signingKey.ExportParameters(includePrivateParameters: false).Modulus!;
        var hmacKey = new SymmetricSecurityKey(modulus) { KeyId = kid };
        var credentials = new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256);
        return CreateToken(issuer, audience, scopes, "test-subject", null, null, credentials);
    }

    /// <summary>alg=none, no signature at all.</summary>
    public static string CreateUnsignedToken(string issuer, string audience, IEnumerable<string> scopes)
    {
        return CreateToken(issuer, audience, scopes, "test-subject", null, null, signingCredentials: null);
    }

    private static string CreateToken(
        string issuer,
        string audience,
        IEnumerable<string> scopes,
        string subject,
        DateTime? notBefore,
        DateTime? expires,
        SigningCredentials? signingCredentials,
        string? email = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("scope", string.Join(' ', scopes)),
        };
        if (email is not null)
            claims.Add(new Claim("email", email));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? now,
            Expires = expires ?? now.AddMinutes(5),
            SigningCredentials = signingCredentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
