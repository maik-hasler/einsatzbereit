using Application.Common.Persistence;
using Application.Engagements;
using Application.Engagements.Common;
using Application.Notifications;
using Domain.Notifications;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.Common;

/// <summary>
/// Notifies affected volunteers and cancels active engagements for an
/// opportunity that is going away or off the public listing - shared by
/// hard delete/shadow delete (<see cref="VolunteerOpportunityDeletionHelper"/>)
/// and the Unpublish/Cancel domain event handlers (einsatzbereit#1038), so all
/// three flows leave volunteers notified and no engagement dangling against a
/// listing that is no longer live.
/// </summary>
internal static class VolunteerOpportunityEngagementCascadeHelper
{
	public static async Task NotifyAndCancelActiveEngagementsAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunityId opportunityId,
		string opportunityTitle,
		NotificationKind opportunityNotificationKind,
		string engagementCancellationReason,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			opportunityNotificationKind,
			cancellationToken);

		// GetActiveEngagementsForOpportunityAsync already excludes anonymized
		// engagements (einsatzbereit#1724), so CancelAndNotifyAsync's own guard below
		// is defense in depth here, not the primary fix.
		var activeEngagements = await dbContext.GetActiveEngagementsForOpportunityAsync(
			opportunityId, cancellationToken);
		foreach (var engagement in activeEngagements)
		{
			// Same notification + email path a single organizer-triggered cancel
			// sends (#1057) - the volunteer shouldn't hear about this only via
			// the opportunity-level notification above.
			await EngagementCancellationHelper.CancelAndNotifyAsync(
				dbContext,
				engagement,
				engagementCancellationReason,
				opportunityTitle,
				logger,
				cancellationToken);
		}
	}
}
