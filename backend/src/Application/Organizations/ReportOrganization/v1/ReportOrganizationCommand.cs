using Application.Common.Messaging;
using Domain.Reports;
using Domain.Users;

namespace Application.Organizations.ReportOrganization.v1;

public sealed record ReportOrganizationCommand(
	Guid OrganizationId,
	UserId ReporterId,
	ReportReason Reason,
	string? Details)
	: ICommand<bool>;
