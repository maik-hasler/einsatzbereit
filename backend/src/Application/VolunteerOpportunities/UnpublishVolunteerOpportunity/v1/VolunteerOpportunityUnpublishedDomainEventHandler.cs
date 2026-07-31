using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Notifications;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.UnpublishVolunteerOpportunity.v1;

// Consumer of VolunteerOpportunityUnpublishedDomainEvent (#1038): the command
// handler only flips Status and raises the event; the engagement
// cascade-cancel + volunteer notification happens here, dispatched by
// OutboxProcessorJob like every other domain event (see EngagementReminderDueHandler
// for the same pattern), so a transient failure (e.g. an email send) is
// retried on the next poll cycle instead of being lost mid-request.
internal sealed class VolunteerOpportunityUnpublishedDomainEventHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<VolunteerOpportunityUnpublishedDomainEventHandler> logger)
	: INotificationHandler<VolunteerOpportunityUnpublishedDomainEvent>
{
	public async Task Handle(
		VolunteerOpportunityUnpublishedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			notification.OpportunityId, cancellationToken);

		if (opportunity is null)
		{
			// Deleted between the Unpublish command committing and the outbox
			// dispatching this event - nothing left to cascade, and retrying
			// would never resolve.
			logger.LogWarning(
				"Skipping unpublish cascade for opportunity {OpportunityId}: it no longer exists",
				notification.OpportunityId.Value);
			return;
		}

		await VolunteerOpportunityEngagementCascadeHelper.NotifyAndCancelActiveEngagementsAsync(
			dbContext,
			engagementReadRepository,
			keycloakUserService,
			emailService,
			emailTemplateRenderer,
			unsubscribeLinkBuilder,
			opportunity,
			notification.OpportunityId,
			NotificationKind.OpportunityUnpublished,
			"Opportunity was unpublished.",
			cancellationToken);
	}
}
