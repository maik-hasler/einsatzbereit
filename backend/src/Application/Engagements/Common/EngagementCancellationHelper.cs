using Application.Common.Exceptions;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.Common;

/// <summary>
/// Cancels an engagement and - unless the caller opts out - creates the same in-app
/// notification that <see cref="CancelEngagement.v1.CancelEngagementCommandHandler"/>
/// creates for an organizer-triggered cancellation, shared so engagements auto-cancelled
/// by an opportunity deletion notify the volunteer identically instead of only via the
/// opportunity-level notification (einsatzbereit#1057). The volunteer's cancellation
/// email itself is not sent here (#1150): Cancel() raises EngagementCancelledDomainEvent,
/// consumed post-commit by EngagementCancelledNotificationHandler, so it fires correctly
/// whether this call happens inside a command's own not-yet-committed transaction or
/// from another already-post-commit domain event handler.
/// </summary>
internal static class EngagementCancellationHelper
{
	/// <param name="notifyVolunteer">
	/// Whether to create the <see cref="NotificationKind.EngagementCancelled"/> row.
	/// False only where the caller has already notified this volunteer about the same
	/// event through another path - see
	/// <see cref="VolunteerOpportunities.Common.VolunteerOpportunityEngagementCascadeHelper"/>
	/// (einsatzbereit#1790). The cancellation itself, and the
	/// EngagementCancelledDomainEvent it raises (and with it the volunteer's email),
	/// happen either way; only the in-app row is skipped.
	/// </param>
	// Returns whether the engagement was actually cancelled - callers that record their
	// own audit trail (CancelEngagementCommandHandler) use this to avoid logging an
	// "EngagementCancelled" entry for an engagement that was in fact left untouched.
	public static async Task<bool> CancelAsync(
		IApplicationDbContext dbContext,
		Engagement engagement,
		string? reason,
		string opportunityTitle,
		bool notifyVolunteer,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		// An anonymized engagement's volunteer has already deleted their account
		// (Engagement.Anonymize()) - Cancel() refuses to touch it (by the same
		// IsAnonymized guard every other mutator uses) and there is no volunteer left to
		// notify, so both the state change and the notification below would be wrong to
		// attempt. Without this guard a single anonymized-but-active engagement (checked
		// in, then its volunteer's account deleted) would 409 every caller of this helper
		// forever, with no way to clear it (einsatzbereit#1724).
		if (engagement.IsAnonymized)
		{
			logger.LogInformation(
				"Skipping cancellation of engagement {EngagementId}: its volunteer has deleted their account, nothing left to cancel or notify.",
				engagement.Id.Value);
			return false;
		}

		engagement.Cancel(reason, opportunityTitle).ThrowIfFailure();

		if (!notifyVolunteer)
			return true;

		var notification = Notification.Create(
			engagement.VolunteerId!.Value,
			NotificationKind.EngagementCancelled,
			engagement.Id.Value,
			opportunityTitle);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);
		return true;
	}
}
