using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CreateEngagement.v1;

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

		await EngagementVolunteerConfirmationHelper.NotifyAsync(
			dbContext,
			keycloakUserService,
			emailService,
			emailTemplateRenderer,
			notification.EngagementId,
			notification.OpportunityId,
			notification.VolunteerId,
			notification.IsSlotSignUp,
			logger,
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
