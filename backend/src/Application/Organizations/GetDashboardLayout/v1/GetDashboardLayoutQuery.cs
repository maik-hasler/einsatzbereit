using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.GetDashboardLayout.v1;

public sealed record GetDashboardLayoutQuery(
	Guid OrganizationId,
	UserId RequestingUserId)
	: IQuery<DashboardLayoutResponse>;
