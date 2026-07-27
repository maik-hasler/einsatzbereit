using Application.Common.Messaging;
using Domain.Users;

namespace Application.Reports.DismissReport.v1;

public sealed record DismissReportCommand(
	Guid ReportId,
	UserId AdminUserId)
	: ICommand<bool>;
