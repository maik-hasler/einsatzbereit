using Application.Common.Exceptions;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;

namespace Application.Engagements.Common;

/// <summary>
/// Cancels an engagement and creates the same in-app notification that
/// <see cref="CancelEngagement.v1.CancelEngagementCommandHandler"/> creates for an
/// organizer-triggered cancellation - shared so engagements auto-cancelled by an
/// opportunity deletion notify the volunteer identically instead of only via the
/// opportunity-level notification (einsatzbereit#1057). The volunteer's cancellation
/// email itself is not sent here (#1150): Cancel() raises EngagementCancelledDomainEvent,
/// consumed post-commit by EngagementCancelledNotificationHandler, so it fires correctly
/// whether this call happens inside a command's own not-yet-committed transaction or
/// from another already-post-commit domain event handler.
/// </summary>
internal static class EngagementCancellationHelper
{
	public static async Task CancelAndNotifyAsync(
		IApplicationDbContext dbContext,
		Engagement engagement,
		string? reason,
		string opportunityTitle,
		CancellationToken cancellationToken)
	{
		engagement.Cancel(reason, opportunityTitle).ThrowIfFailure();

		var notification = Notification.Create(
			engagement.VolunteerId!.Value,
			NotificationKind.EngagementCancelled,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);
	}
}
