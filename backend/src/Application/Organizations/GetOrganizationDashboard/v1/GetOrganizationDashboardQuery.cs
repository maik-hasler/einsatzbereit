using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.GetOrganizationDashboard.v1;

public sealed record GetOrganizationDashboardQuery(
	Guid OrganizationId,
	UserId RequestingUserId)
	: IQuery<OrganizationDashboardResponse?>;
