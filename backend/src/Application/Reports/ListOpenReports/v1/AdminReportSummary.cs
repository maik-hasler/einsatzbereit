namespace Application.Reports.ListOpenReports.v1;

public sealed record AdminReportSummary(
	Guid Id,
	string TargetType,
	Guid TargetId,
	string TargetTitle,
	string Reason,
	string? Details,
	DateTimeOffset CreatedOn);
