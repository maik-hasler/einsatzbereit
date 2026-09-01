namespace Application.Organizations.GetOrganizationDashboard.v1;

/// <summary>
/// The counts the organizer dashboard's summary tiles are built from.
/// </summary>
/// <param name="PendingEngagements">Sign-ups still waiting for an organizer's decision.</param>
/// <param name="ConfirmedEngagementsTotal">Confirmed sign-ups, all time - rows, not people.</param>
/// <param name="DistinctVolunteersTotal">
/// People behind those confirmed sign-ups. One helper who takes twelve slots counts once.
/// </param>
/// <param name="SignUpsLast30Days">Sign-ups received in the last 30 days, whatever became of them.</param>
/// <param name="SignUpsPrevious30Days">The same count for the 30 days before that, so the tile can compare.</param>
public sealed record OrganizationDashboardResponse(
	int PendingEngagements,
	int ConfirmedEngagementsTotal,
	int DistinctVolunteersTotal,
	int SignUpsLast30Days,
	int SignUpsPrevious30Days);
