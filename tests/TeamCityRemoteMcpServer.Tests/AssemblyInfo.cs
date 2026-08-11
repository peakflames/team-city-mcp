using Xunit;

// TestServerFactory and AuthEnabledTestServerFactory both configure the app under test via
// process-wide environment variables (TEAM_CITY_URL, McpAuth__*, ASPNETCORE_ENVIRONMENT) — safe
// only if test classes never run concurrently with each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
