namespace Application.Reports.ListFlaggedTargets.v1;

public sealed record FlaggedTargetSummary(
	string TargetType,
	Guid TargetId,
	string TargetTitle,
	int OpenReportCount,
	int TotalReportCount,
	DateTimeOffset LastReportedOn,
	bool IsDeleted);
