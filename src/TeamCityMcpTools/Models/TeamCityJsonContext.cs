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
[JsonSerializable(typeof(ProjectListResponse))]
[JsonSerializable(typeof(ProjectSummary))]
[JsonSerializable(typeof(ProjectDetails))]
[JsonSerializable(typeof(ParentProjectRef))]
[JsonSerializable(typeof(BuildTypeListResponse))]
[JsonSerializable(typeof(BuildTypeSummary))]
[JsonSerializable(typeof(BuildTypeDetails))]
[JsonSerializable(typeof(VcsRootsWrapper))]
[JsonSerializable(typeof(VcsRootEntry))]
[JsonSerializable(typeof(VcsRootInfo))]
public partial class TeamCityJsonContext : JsonSerializerContext
{
}
