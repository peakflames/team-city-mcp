using Microsoft.Extensions.Hosting;
using TeamCityRemoteMcpServer.Auth;
using Xunit;

namespace TeamCityRemoteMcpServer.Tests;

public class McpAuthOptionsValidatorTests
{
    private static McpAuthOptionsValidator CreateValidator(bool isDevelopment) =>
        new(new FakeHostEnvironment(isDevelopment));

    private static McpAuthOptions ValidOptions() => new()
    {
        Enabled = true,
        Issuer = "https://issuer.okta.example.invalid/oauth2/aus123abc",
        ResourceUri = "https://teamcity-mcp.example.invalid/mcp",
        ScopesSupported = [OAuthScopes.Read],
        ClockSkewSeconds = 30,
    };

    [Fact]
    public void Validate_SkipsAllChecks_WhenDisabled()
    {
        var validator = CreateValidator(isDevelopment: false);
        var options = new McpAuthOptions { Enabled = false, Issuer = "not a uri" };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Succeeds_ForWellFormedOptions()
    {
        var validator = CreateValidator(isDevelopment: false);

        var result = validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Allows_IssuerWithPath()
    {
        // A Custom Authorization Server's issuer legitimately has a path — unlike an embedded
        // issuer, this must not be rejected.
        var validator = CreateValidator(isDevelopment: false);
        var options = ValidOptions();
        options.Issuer = "https://issuer.okta.example.invalid/oauth2/ausABCDEF123";

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Rejects_HttpIssuerOutsideDevelopment()
    {
        var validator = CreateValidator(isDevelopment: false);
        var options = ValidOptions();
        options.Issuer = "http://insecure.example.invalid";

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_Allows_HttpIssuerInDevelopment()
    {
        var validator = CreateValidator(isDevelopment: true);
        var options = ValidOptions();
        options.Issuer = "http://127.0.0.1:5199";

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Rejects_NonAbsoluteResourceUri()
    {
        var validator = CreateValidator(isDevelopment: false);
        var options = ValidOptions();
        options.ResourceUri = "/mcp";

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_Rejects_ClockSkewOutOfRange()
    {
        var validator = CreateValidator(isDevelopment: false);
        var options = ValidOptions();
        options.ClockSkewSeconds = 301;

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(bool isDevelopment)
        {
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "TeamCityRemoteMcpServer.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
