using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Notifications;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.CancelVolunteerOpportunity.v1;

// Consumer of VolunteerOpportunityCancelledDomainEvent (#1038): the command
// handler only flips Status and raises the event; the engagement
// cascade-cancel + volunteer notification happens here, dispatched by
// OutboxProcessorJob like every other domain event (see EngagementReminderDueHandler
// for the same pattern), so a transient failure (e.g. an email send) is
// retried on the next poll cycle instead of being lost mid-request.
//
// Publisher.Publish() resolves this handler from its own fresh child scope
// (see Application/Common/Messaging/Publisher.cs), not the scope
// OutboxProcessorJob itself is running in - so the IApplicationDbContext
// injected here is a *different* DbContext instance than the one
// OutboxProcessorJob.ProcessBatchAsync later calls SaveChangesAsync on.
// Nothing else persists this handler's writes (Engagement.Cancel(), the new
// Notification rows), so it must call SaveChangesAsync itself via IUnitOfWork
// (both resolve to the same ApplicationDbContext instance within this scope -
// see Infrastructure/ServiceCollectionExtensions.cs).
internal sealed class VolunteerOpportunityCancelledDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IEngagementReadRepository engagementReadRepository,
	ILogger<VolunteerOpportunityCancelledDomainEventHandler> logger)
	: INotificationHandler<VolunteerOpportunityCancelledDomainEvent>
{
	public async Task Handle(
		VolunteerOpportunityCancelledDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			notification.OpportunityId, cancellationToken);

		if (opportunity is null)
		{
			// Deleted between the Cancel command committing and the outbox
			// dispatching this event - nothing left to cascade, and retrying
			// would never resolve.
			logger.LogWarning(
				"Skipping cancel cascade for opportunity {OpportunityId}: it no longer exists",
				notification.OpportunityId.Value);
			return;
		}

		var engagementCancellationReason = string.IsNullOrWhiteSpace(notification.Reason)
			? "Opportunity was cancelled."
			: $"Opportunity was cancelled: {notification.Reason}";

		await VolunteerOpportunityEngagementCascadeHelper.NotifyAndCancelActiveEngagementsAsync(
			dbContext,
			engagementReadRepository,
			notification.OpportunityId,
			opportunity.TitleDe,
			NotificationKind.OpportunityCancelled,
			engagementCancellationReason,
			// The OpportunityCancelled notification above already tells the volunteer
			// their sign-up is gone, so a second EngagementCancelled row per volunteer
			// only repeated the same fact and inflated the unread badge (#1790).
			notifyPerEngagement: false,
			logger,
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
