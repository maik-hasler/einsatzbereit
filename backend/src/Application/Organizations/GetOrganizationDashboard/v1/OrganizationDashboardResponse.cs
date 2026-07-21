namespace Application.Organizations.GetOrganizationDashboard.v1;

public sealed record OrganizationDashboardResponse(
	int OpenOpportunities,
	int PendingEngagements,
	int ConfirmedEngagementsNext7Days,
	int ConfirmedEngagementsTotal,
	int CancelledEngagements);
