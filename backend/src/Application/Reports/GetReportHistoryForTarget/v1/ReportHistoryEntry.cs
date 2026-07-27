namespace Application.Reports.GetReportHistoryForTarget.v1;

public sealed record ReportHistoryEntry(
	Guid Id,
	Guid ReporterId,
	string Reason,
	string? Details,
	string Status,
	DateTimeOffset CreatedOn,
	Guid? ResolvedByUserId,
	DateTimeOffset? ResolvedOn);
