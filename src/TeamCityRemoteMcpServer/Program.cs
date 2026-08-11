namespace TeamCityRemoteMcpServer;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logDir, "TeamCityRemoteMcpServer_.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug()
                .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger();

            Console.WriteLine("Booting TeamCityRemoteMcpServer...");
            Console.WriteLine($"Logs will be written to: {logDir}");

            var app = BuildApp(args);

            Log.Information("Starting TeamCityRemoteMcpServer...");
            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log.Fatal($"Host terminated unexpectedly. Exception: {ex}");
            Console.ResetColor();
            return 1;
        }
    }

    public static WebApplication BuildApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Resolve config — env vars take precedence over appsettings
        var serverUrl   = Environment.GetEnvironmentVariable("TEAM_CITY_URL")
                          ?? builder.Configuration["TeamCityConfig:ServerUrl"]
                          ?? string.Empty;
        var accessToken = Environment.GetEnvironmentVariable("TEAM_CITY_ACCESS_TOKEN") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException(
                "TeamCity server URL is not configured. " +
                "Set the TEAM_CITY_URL environment variable or TeamCityConfig:ServerUrl in appsettings.");

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException(
                "TeamCity access token is not configured. " +
                "Set the TEAM_CITY_ACCESS_TOKEN environment variable.");

        Log.Information("Loaded TeamCity configuration for server: {ServerUrl}", serverUrl);

        builder.Services.AddSerilog();
        builder.Services.AddSingleton(new TeamCityConfig(serverUrl, accessToken));
        builder.Services.AddHttpClient<ITeamCityClientFactory, TeamCityRemoteClientFactory>((sp, client) =>
        {
            var config = sp.GetRequiredService<TeamCityConfig>();
            client.BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        var mcpBuilder = builder.Services
            .AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithTools<BuildTools>()
            .WithTools<ProjectTools>();

        var authEnabled = builder.AddMcpAuth(mcpBuilder);

        var app = builder.Build();

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
        });

        // UseRouting must run before UseAuthentication/UseAuthorization so that endpoint
        // metadata (RequireAuthorization) is available by the time the authorization middleware
        // runs — without an explicit UseRouting call here, the implicit routing insertion point
        // lands at the first Map* call, which is after these two and would silently turn
        // authorization into a no-op (every request reaches the endpoint unauthenticated).
        app.UseRouting();

        // Stateless transport maps POST-only streamable HTTP; the legacy fake-SSE GET
        // workaround for Cline/TypeScript SDK is gone along with /sse.
        if (authEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapMcp("mcp").RequireAuthorization(OAuthScopes.ReadPolicy);
        }
        else
        {
            app.MapMcp("mcp");
        }

        return app;
    }
}
