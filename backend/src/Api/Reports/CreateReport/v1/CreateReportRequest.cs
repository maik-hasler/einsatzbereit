using System.ComponentModel.DataAnnotations;

namespace Api.Reports.CreateReport.v1;

public sealed record CreateReportRequest(
	Guid ContentId,
	string ContentType,
	string Reason,
	[MaxLength(1000)] string? Detail);
