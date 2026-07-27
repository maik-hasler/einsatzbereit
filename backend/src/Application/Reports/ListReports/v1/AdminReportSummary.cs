namespace Application.Reports.ListReports.v1;

public sealed record AdminReportSummary(
	Guid Id,
	string ContentType,
	Guid ContentId,
	string? ContentTitle,
	Guid ReporterId,
	string Reason,
	string? Detail,
	string Status,
	DateTimeOffset CreatedOn);
