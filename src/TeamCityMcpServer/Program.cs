using TeamCityMcpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TeamCityMcpServer;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.File(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "TeamCityMcpServer_.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug()
                .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger();

            Log.Information("TeamCity MCP Server starting...");

            string? GetConfigValue(string[] cliArgs, string cliParamShort, string cliParamLong, string envVarName)
            {
                for (int i = 0; i < cliArgs.Length; i++)
                {
                    if ((string.Equals(cliArgs[i], cliParamShort, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cliArgs[i], cliParamLong, StringComparison.OrdinalIgnoreCase)) &&
                        i + 1 < cliArgs.Length)
                    {
                        return cliArgs[i + 1];
                    }
                }
                return Environment.GetEnvironmentVariable(envVarName);
            }

            string? serverUrl   = GetConfigValue(args, "-u", "--url",   "TEAM_CITY_URL");
            string? accessToken = GetConfigValue(args, "-t", "--token", "TEAM_CITY_ACCESS_TOKEN");

            bool configMissing = false;
            if (string.IsNullOrEmpty(serverUrl))
            {
                Log.Error("TeamCity server URL not provided. Use -u/--url <value> or set TEAM_CITY_URL environment variable.");
                configMissing = true;
            }
            if (string.IsNullOrEmpty(accessToken))
            {
                Log.Error("TeamCity access token not provided. Use -t/--token <value> or set TEAM_CITY_ACCESS_TOKEN environment variable.");
                configMissing = true;
            }

            if (configMissing)
            {
                Log.Error("Skipping TeamCity API interaction due to missing configuration. Please provide all required parameters.");
                return 1;
            }

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSerilog();
            builder.Services.AddSingleton(new TeamCityConfig(serverUrl!, accessToken!));
            builder.Services.AddScoped<ITeamCityClientFactory, TeamCityClientFactory>();

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<BuildTools>();

            Log.Information("Starting TeamCityMcpServer...");
            builder.Build().Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal($"Host terminated unexpectedly. Exception: {ex}");
            return 1;
        }
    }
}
