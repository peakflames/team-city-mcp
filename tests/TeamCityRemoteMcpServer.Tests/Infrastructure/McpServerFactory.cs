namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Drives Program.BuildApp directly with UseTestServer(), rather than through
/// WebApplicationFactory&lt;Program&gt;'s reflection-based host resolution — proven by
/// ConfigPlumbingCanaryTests to be the only path where ConfigureAppConfiguration additions
/// are visible to BuildApp's imperative config read, which runs before builder.Build().
/// </summary>
public class McpServerFactory : IDisposable
{
    private readonly Dictionary<string, string?> _configValues = new(StringComparer.Ordinal);
    private Action<IServiceCollection>? _configureServices;
    private Action<IServiceCollection>? _postAuthConfigureServices;
    private string? _environmentName;
    private WebApplication? _app;

    public McpServerFactory With(string key, string? value)
    {
        _configValues[key] = value;
        return this;
    }

    /// <summary>McpAuthOptionsValidator only allows a plaintext http Issuer in Development.
    /// Passed as a `--environment` command-line argument to Program.BuildApp, not set via
    /// builder.Environment.EnvironmentName after the fact — WebApplicationBuilder combined with
    /// WebHost.UseTestServer() re-derives IHostEnvironment from host configuration during Build(),
    /// silently discarding a post-hoc mutation of the Environment property. The command-line
    /// switch is read during WebApplication.CreateBuilder(args) itself, before anything else runs,
    /// so it is not subject to that override.</summary>
    public McpServerFactory WithEnvironment(string environmentName)
    {
        _environmentName = environmentName;
        return this;
    }

    public McpServerFactory WithMcpAuth(Action<Dictionary<string, string?>> configure)
    {
        configure(_configValues);
        return this;
    }

    public McpServerFactory WithTestServices(Action<IServiceCollection> configure)
    {
        _configureServices = _configureServices is null ? configure : _configureServices + configure;
        return this;
    }

    /// <summary>Runs after <c>AddMcpAuth</c>/<c>AddRbac</c>, unlike <see cref="WithTestServices"/> —
    /// the only seam that can substitute a service AddRbac's own <c>Replace()</c> call would
    /// otherwise clobber (e.g. <c>IPermissionGate</c>).</summary>
    public McpServerFactory WithPostAuthServices(Action<IServiceCollection> configure)
    {
        _postAuthConfigureServices = _postAuthConfigureServices is null ? configure : _postAuthConfigureServices + configure;
        return this;
    }

    /// <summary>Substitutes TimeProvider with the given FakeTimeProvider. Works because this
    /// registration (via the `configure` callback) is enumerated before AddMcpAuth's
    /// TryAddSingleton(TimeProvider.System) — TryAdd is a no-op once a TimeProvider registration
    /// already exists.</summary>
    public McpServerFactory WithFakeTime(FakeTimeProvider time) =>
        WithTestServices(services => services.AddSingleton<TimeProvider>(time));

    public IServiceProvider Services => GetOrBuildApp().Services;

    public HttpClient CreateClient() => GetOrBuildApp().GetTestClient();

    private WebApplication GetOrBuildApp()
    {
        if (_app is not null)
            return _app;

        var args = _environmentName is not null
            ? new[] { "--environment", _environmentName }
            : [];

        _app = Program.BuildApp(
            args,
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(_configValues);
                if (_configureServices is not null)
                    _configureServices(builder.Services);
            },
            builder =>
            {
                if (_postAuthConfigureServices is not null)
                    _postAuthConfigureServices(builder.Services);
            });

        _app.Start();
        return _app;
    }

    public void Dispose()
    {
        if (_app is null)
            return;

        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().GetAwaiter().GetResult();
    }
}
