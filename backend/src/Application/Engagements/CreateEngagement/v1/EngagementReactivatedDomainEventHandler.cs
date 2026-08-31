using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CreateEngagement.v1;

internal sealed class EngagementReactivatedDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	ILogger<EngagementReactivatedDomainEventHandler> logger)
	: INotificationHandler<EngagementReactivatedDomainEvent>
{
	public async Task Handle(
		EngagementReactivatedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		await EngagementOrganizerNotificationHelper.EnqueueAsync(
			dbContext,
			keycloakOrganizationService,
			keycloakUserService,
			notification.EngagementId,
			notification.OpportunityId,
			notification.VolunteerId,
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
