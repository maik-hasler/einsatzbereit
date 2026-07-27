using Application.Common.Messaging;
using Domain.Users;

namespace Application.Reports.ResolveReport.v1;

public sealed record ResolveReportCommand(
	Guid ReportId,
	UserId ActingUserId)
	: ICommand<bool>;
