using Application.Common.Messaging;
using Domain.Reports;
using Domain.Users;

namespace Application.Reports.CreateReport.v1;

public sealed record CreateReportCommand(
	Guid ContentId,
	ReportedContentType ContentType,
	UserId ReporterId,
	ReportReason Reason,
	string? Detail)
	: ICommand<Guid>;
