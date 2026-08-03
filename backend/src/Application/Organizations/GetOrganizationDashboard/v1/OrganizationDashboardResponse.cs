namespace Application.Organizations.GetOrganizationDashboard.v1;

public sealed record OrganizationDashboardResponse(
	int PendingEngagements,
	int ConfirmedEngagementsTotal);
