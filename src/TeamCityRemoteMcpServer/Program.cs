using TeamCityMcpTools;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

namespace TeamCityRemoteMcpServer;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    Path.Combine(logDir, "TeamCityRemoteMcpServer_.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug()
                .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger();

            Console.WriteLine("Booting TeamCityRemoteMcpServer...");
            Console.WriteLine($"Logs will be written to: {logDir}");

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
            builder.Services.AddScoped<ITeamCityClientFactory, TeamCityRemoteClientFactory>();

            builder.Services
                .AddMcpServer()
                .WithHttpTransport();

            var app = builder.Build();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
            });

            // SSE stream disconnection workaround for Cline/TypeScript MCP SDK (streamableHttp only).
            // The TypeScript MCP SDK has a bug where GET requests wait in a loop that can timeout.
            // This middleware intercepts GET requests to streamableHttp endpoints and sends a dummy response.
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value;
                if (context.Request.Method == "GET" &&
                    path != null &&
                    !path.EndsWith("/sse") &&
                    !path.EndsWith("/message") &&
                    !path.Equals("/", StringComparison.Ordinal))
                {
                    Log.Debug("StreamableHttp workaround: Intercepting GET {Path}", context.Request.Path);
                    context.Response.ContentType = "text/event-stream";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers.Connection = "keep-alive";
                    const string fakeResponseJson = """{"id":0,"jsonrpc":"2.0","result":{}}""";
                    await context.Response.WriteAsync($"event: message\ndata: {fakeResponseJson}\n\n");
                    return;
                }
                await next();
            });

            app.MapMcp();        // Root: /, /sse (default SSE transport)
            app.MapMcp("mcp");   // /mcp (streamable HTTP transport for Cline)

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
}
