using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.WithdrawEngagement.v1;

internal sealed class WithdrawEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<WithdrawEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		WithdrawEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		if (engagement.IsAnonymized)
			throw new ResultFailureException(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer account has been deleted."));

		if (engagement.VolunteerId!.Value.Value != request.VolunteerId)
			throw new ResultFailureException(Error.Forbidden("Engagement.NotOwner", "Only the volunteer who created this engagement can withdraw it."));

		engagement.Withdraw().ThrowIfFailure();

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			engagement.OpportunityId, cancellationToken);

		if (opportunity is not null)
		{
			var members = await keycloakOrganizationService
				.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);

			foreach (var organizer in members.Where(m => m.IsOrganisator))
			{
				// The organizer withdrawal email is NOT sent here (#1174): it moves
				// off this request's DB transaction onto the outbox, delivered by
				// EngagementWithdrawnDomainEventHandler once
				// EngagementWithdrawnDomainEvent (raised above by Engagement.Withdraw)
				// is dispatched - see EngagementOrganizerNotificationHelper.
				var notification = Notification.Create(
					UserId.Create(organizer.UserId).GetValueOrThrow(),
					NotificationKind.EngagementWithdrawn,
					engagement.Id.Value);

				await dbContext.Notifications.AddAsync(notification, cancellationToken);
			}
		}

		return engagement;
	}
}
