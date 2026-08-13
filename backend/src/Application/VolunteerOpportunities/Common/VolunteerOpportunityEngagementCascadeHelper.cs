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
	/// <param name="notifyPerEngagement">
	/// Whether each cancelled engagement also gets its own
	/// <see cref="NotificationKind.EngagementCancelled"/> notification on top of the
	/// opportunity-level one. False only for the cancellation flow, whose
	/// <see cref="NotificationKind.OpportunityCancelled"/> text already tells the volunteer
	/// their sign-up is gone, so the second row was pure duplication
	/// (einsatzbereit#1790). The delete and unpublish flows keep it: their
	/// opportunity-level texts do not fully cover the sign-up outcome.
	/// </param>
	public static async Task NotifyAndCancelActiveEngagementsAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunityId opportunityId,
		string opportunityTitle,
		NotificationKind opportunityNotificationKind,
		string engagementCancellationReason,
		bool notifyPerEngagement,
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
		// engagements (einsatzbereit#1724), so CancelAsync's own guard below
		// is defense in depth here, not the primary fix.
		var activeEngagements = await dbContext.GetActiveEngagementsForOpportunityAsync(
			opportunityId, cancellationToken);
		foreach (var engagement in activeEngagements)
		{
			// Same cancellation + email path a single organizer-triggered cancel takes
			// (#1057). Whether the volunteer also gets a per-engagement in-app
			// notification is the caller's call: NotifyActiveVolunteersAsync above
			// already reached exactly this set of volunteers (both reads filter active,
			// non-anonymized engagements of the opportunity by the same predicate), so
			// a flow whose opportunity-level text already spells the sign-up outcome
			// out would otherwise say the same thing twice (einsatzbereit#1790). Either
			// way Cancel() raises EngagementCancelledDomainEvent, so the cancellation
			// email still goes out.
			await EngagementCancellationHelper.CancelAsync(
				dbContext,
				engagement,
				engagementCancellationReason,
				opportunityTitle,
				notifyPerEngagement,
				logger,
				cancellationToken);
		}
	}
}
