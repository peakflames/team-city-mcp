namespace TeamCityRemoteMcpServer.Auth;

/// <summary>
/// Accumulates every validation failure into one result. Returns Success immediately when
/// McpAuth is disabled so a malformed McpAuth section can never break a server with auth off.
/// </summary>
public sealed class McpAuthOptionsValidator : IValidateOptions<McpAuthOptions>
{
    private readonly IHostEnvironment _environment;

    public McpAuthOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, McpAuthOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();
        var isDevelopment = _environment.IsDevelopment();

        ValidateIssuer(options, isDevelopment, failures);
        ValidateResourceUri(options, failures);
        ValidateMetadataAddress(options, isDevelopment, failures);
        ValidateClockSkew(options, failures);
        ValidateClientBinding(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateIssuer(McpAuthOptions options, bool isDevelopment, List<string> failures)
    {
        if (!TryParseAbsoluteUri(options.Issuer, out var issuerUri))
        {
            failures.Add("Issuer must be an absolute URI.");
            return;
        }

        if (!IsAllowedScheme(issuerUri, isDevelopment))
        {
            failures.Add("Issuer must use https (http is only allowed in the Development environment).");
        }

        // Unlike an embedded issuer, an external Custom Authorization Server's issuer legitimately
        // has a path (e.g. an Okta Custom AS: https://issuer.okta.example.invalid/oauth2/<asid>), so a
        // path is not itself an error.
        if (!string.IsNullOrEmpty(issuerUri.Fragment))
        {
            failures.Add("Issuer must not contain a fragment.");
        }
    }

    private static void ValidateResourceUri(McpAuthOptions options, List<string> failures)
    {
        if (!TryParseAbsoluteUri(options.ResourceUri, out var resourceUri))
        {
            failures.Add("ResourceUri must be an absolute URI.");
            return;
        }

        if (!string.IsNullOrEmpty(resourceUri.Fragment))
        {
            failures.Add("ResourceUri must not contain a fragment.");
        }
    }

    private static void ValidateMetadataAddress(McpAuthOptions options, bool isDevelopment, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.MetadataAddress))
            return;

        if (!TryParseAbsoluteUri(options.MetadataAddress, out var metadataUri))
        {
            failures.Add("MetadataAddress must be an absolute URI.");
            return;
        }

        if (!IsAllowedScheme(metadataUri, isDevelopment))
        {
            failures.Add("MetadataAddress must use https (http is only allowed in the Development environment).");
        }
    }

    private static void ValidateClockSkew(McpAuthOptions options, List<string> failures)
    {
        if (options.ClockSkewSeconds < 0 || options.ClockSkewSeconds > 300)
            failures.Add("ClockSkewSeconds must be between 0 and 300.");
    }

    /// <summary>
    /// The one combination that must never boot: audience binding off *and* no client allowlist.
    /// That pair accepts any token the authorization server issued to any client in the org for any
    /// purpose — no issuer-level check distinguishes a token minted for this MCP server from one
    /// minted for an unrelated internal app. Enforced here rather than per-request so the failure is
    /// a refused startup, not a server that reports healthy while accepting foreign tokens.
    /// </summary>
    private static void ValidateClientBinding(McpAuthOptions options, List<string> failures)
    {
        var allowed = options.AllowedClientIds;

        if (!options.ValidateAudience && (allowed is null || allowed.Count == 0))
        {
            failures.Add(
                "AllowedClientIds must contain at least one client id when ValidateAudience is false — " +
                "disabling audience binding with no client allowlist would accept any token the " +
                "authorization server issued to any client.");
        }

        if (allowed is null)
            return;

        for (var i = 0; i < allowed.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(allowed[i]))
                failures.Add($"AllowedClientIds[{i}] must not be blank.");
        }
    }

    private static bool TryParseAbsoluteUri(string value, out Uri uri)
    {
        var ok = Uri.TryCreate(value, UriKind.Absolute, out var parsed);
        uri = parsed ?? new Uri("about:blank");
        return ok;
    }

    private static bool IsAllowedScheme(Uri uri, bool isDevelopment)
    {
        if (string.Equals(uri.Scheme, "https", StringComparison.Ordinal))
            return true;

        return isDevelopment && string.Equals(uri.Scheme, "http", StringComparison.Ordinal);
    }
}
