using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CreateEngagement.v1;

// Consumer of EngagementCreatedDomainEvent (#1174): the organizer "New sign-up"
// email used to be sent inline inside CreateEngagementCommandHandler's DB
// transaction, once per organizer. Delivery now happens here, dispatched by
// OutboxProcessorJob like every other domain event, well after the triggering
// command's transaction has committed - see EngagementOrganizerNotificationHelper
// for the full rationale.
internal sealed class EngagementCreatedDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementCreatedDomainEventHandler> logger)
	: INotificationHandler<EngagementCreatedDomainEvent>
{
	public async Task Handle(
		EngagementCreatedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		await EngagementOrganizerNotificationHelper.NotifyAsync(
			dbContext,
			keycloakOrganizationService,
			keycloakUserService,
			emailService,
			emailTemplateRenderer,
			unsubscribeLinkBuilder,
			notification.EngagementId,
			notification.OpportunityId,
			notification.VolunteerId,
			EmailTemplateKind.EngagementSignupNotifyOrganizer,
			EmailNotificationType.NewSignUp,
			logger,
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
