using Application.Common.Messaging;
using Domain.Reports;
using Domain.Users;

namespace Application.Users.ReportUser.v1;

public sealed record ReportUserCommand(
	Guid UserId,
	UserId ReporterId,
	ReportReason Reason,
	string? Details)
	: ICommand<bool>;
