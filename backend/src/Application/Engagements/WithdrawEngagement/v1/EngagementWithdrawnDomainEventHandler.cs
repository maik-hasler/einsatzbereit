using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.WithdrawEngagement.v1;

internal sealed class EngagementWithdrawnDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	ILogger<EngagementWithdrawnDomainEventHandler> logger)
	: INotificationHandler<EngagementWithdrawnDomainEvent>
{
	public async Task Handle(
		EngagementWithdrawnDomainEvent notification,
		CancellationToken cancellationToken)
	{
		await EngagementOrganizerNotificationHelper.EnqueueAsync(
			dbContext,
			keycloakOrganizationService,
			keycloakUserService,
			notification.EngagementId,
			notification.OpportunityId,
			notification.VolunteerId,
			EmailNotificationType.Withdrawal,
			logger,
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
