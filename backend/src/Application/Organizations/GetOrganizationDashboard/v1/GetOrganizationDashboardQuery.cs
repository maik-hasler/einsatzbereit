using Application.Common.Messaging;

namespace Application.Organizations.GetOrganizationDashboard.v1;

public sealed record GetOrganizationDashboardQuery(
	Guid OrganizationId)
	: IQuery<OrganizationDashboardResponse?>;
