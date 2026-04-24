namespace TeamCityMcpTools.Models;

[JsonSerializable(typeof(BuildListResponse))]
[JsonSerializable(typeof(BuildSummary))]
[JsonSerializable(typeof(BuildDetails))]
[JsonSerializable(typeof(BuildTypeInfo))]
[JsonSerializable(typeof(AgentInfo))]
[JsonSerializable(typeof(TriggeredInfo))]
[JsonSerializable(typeof(TriggeredUser))]
[JsonSerializable(typeof(RevisionsWrapper))]
[JsonSerializable(typeof(RevisionInfo))]
[JsonSerializable(typeof(ProblemOccurrencesWrapper))]
[JsonSerializable(typeof(ProblemOccurrence))]
public partial class TeamCityJsonContext : JsonSerializerContext
{
}
