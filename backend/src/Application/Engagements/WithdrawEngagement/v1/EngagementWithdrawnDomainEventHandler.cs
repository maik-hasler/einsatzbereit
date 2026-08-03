using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.WithdrawEngagement.v1;

// Consumer of EngagementWithdrawnDomainEvent (#1174): the organizer withdrawal
// email used to be sent inline inside WithdrawEngagementCommandHandler's DB
// transaction, once per organizer. Mirrors
// EngagementCreatedDomainEventHandler/EngagementReactivatedDomainEventHandler -
// see EngagementOrganizerNotificationHelper for the full rationale.
internal sealed class EngagementWithdrawnDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementWithdrawnDomainEventHandler> logger)
	: INotificationHandler<EngagementWithdrawnDomainEvent>
{
	public async Task Handle(
		EngagementWithdrawnDomainEvent notification,
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
			EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
			EmailNotificationType.Withdrawal,
			logger,
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
