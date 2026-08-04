using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.SubmitFeedback.v1;

// Consumer of EngagementFeedbackSubmittedDomainEvent (#1047): SubmitFeedbackCommandHandler only
// records the rating/comment on the engagement and raises the event; letting the opportunity's
// organizers know feedback came in happens here, dispatched by OutboxProcessorJob like every
// other domain event.
internal sealed class EngagementFeedbackSubmittedDomainEventHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	ILogger<EngagementFeedbackSubmittedDomainEventHandler> logger)
	: INotificationHandler<EngagementFeedbackSubmittedDomainEvent>
{
	public async Task Handle(
		EngagementFeedbackSubmittedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken);
		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping feedback notification for opportunity {OpportunityId}: it no longer exists",
				notification.OpportunityId.Value);
			return;
		}

		var members = await keycloakOrganizationService.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);
		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var inAppNotification = Notification.Create(
				UserId.Create(organizer.UserId).GetValueOrThrow(),
				NotificationKind.FeedbackSubmitted,
				notification.EngagementId.Value);
			await dbContext.Notifications.AddAsync(inAppNotification, cancellationToken);
		}

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
