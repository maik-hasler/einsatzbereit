using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CreateEngagement.v1;

// Consumer of EngagementReactivatedDomainEvent (#1174): a withdrawn/cancelled
// engagement is reused via Engagement.Reactivate (called from
// CreateEngagementCommandHandler) rather than inserting a new row, but it still
// deserves the same organizer "New sign-up" email a genuinely new engagement
// gets. Mirrors EngagementCreatedDomainEventHandler - see
// EngagementOrganizerNotificationHelper for the full rationale.
internal sealed class EngagementReactivatedDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementReactivatedDomainEventHandler> logger)
	: INotificationHandler<EngagementReactivatedDomainEvent>
{
	public async Task Handle(
		EngagementReactivatedDomainEvent notification,
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
