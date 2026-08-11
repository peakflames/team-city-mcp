namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// The one rule that matters most for Rbac: enabling it without McpAuth means no caller identity
/// for anyone, which fails closed for every caller — a 100% denial rate that looks like an outage.
/// Refusing to boot turns that into an actionable startup error instead.
/// </summary>
public class RbacOptionsValidationTests
{
    [Fact]
    public void RbacEnabled_WithMcpAuthDisabled_FailsToBoot()
    {
        using var factory = new McpServerFactory()
            .With("TEAM_CITY_URL", "https://teamcity.example.invalid")
            .With("TEAM_CITY_ACCESS_TOKEN", "test-token");
        factory.WithRbacEnabled();

        Assert.Throws<OptionsValidationException>(() => _ = factory.Services);
    }

    [Fact]
    public void RbacEnabled_WithMcpAuthEnabled_BootsSuccessfully()
    {
        using var mcpAuth = new McpAuthTestConfigBuilder();
        using var factory = new McpServerFactory()
            .With("TEAM_CITY_URL", "https://teamcity.example.invalid")
            .With("TEAM_CITY_ACCESS_TOKEN", "test-token");
        mcpAuth.Apply(factory).WithRbacEnabled();

        var options = factory.Services.GetRequiredService<IOptions<RbacOptions>>().Value;

        Assert.True(options.Enabled);
        var gate = factory.Services.GetRequiredService<IPermissionGate>();
        Assert.IsType<AlwaysAllowPermissionGate>(gate);
    }

    [Fact]
    public void RbacDisabled_WithMcpAuthDisabled_BootsSuccessfully()
    {
        using var factory = new McpServerFactory()
            .With("TEAM_CITY_URL", "https://teamcity.example.invalid")
            .With("TEAM_CITY_ACCESS_TOKEN", "test-token");

        var options = factory.Services.GetRequiredService<IOptions<RbacOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.IsType<NoOpPermissionGate>(factory.Services.GetRequiredService<IPermissionGate>());
    }

    [Fact]
    public void RbacEnabled_BlankIdentityClaim_FailsToBoot()
    {
        using var mcpAuth = new McpAuthTestConfigBuilder();
        using var factory = new McpServerFactory()
            .With("TEAM_CITY_URL", "https://teamcity.example.invalid")
            .With("TEAM_CITY_ACCESS_TOKEN", "test-token");
        mcpAuth.Apply(factory).WithRbacEnabled().With("Rbac:IdentityClaim", "");

        Assert.Throws<OptionsValidationException>(() => _ = factory.Services);
    }
}
