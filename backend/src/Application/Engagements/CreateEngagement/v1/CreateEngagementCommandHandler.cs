using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.CreateEngagement.v1;

internal sealed class CreateEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<CreateEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CreateEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			request.OpportunityId, cancellationToken);

		if (opportunity is null)
			throw new DomainException($"Volunteer opportunity with id '{request.OpportunityId.Value}' was not found.");

		var alreadySignedUp = await dbContext.HasEngagementAsync(
			request.VolunteerId, request.OpportunityId, cancellationToken);

		if (alreadySignedUp)
			throw new DomainException("Conflict: you are already signed up for this opportunity.");

		var engagement = request.TimeSlotId is not null
			? Engagement.CreateWaitlistSignUp(request.OpportunityId, request.VolunteerId, request.TimeSlotId.Value)
			: Engagement.CreateIndividualContact(request.OpportunityId, request.VolunteerId, request.Message
				?? throw new DomainException("Message is required for individual contact."));

		await dbContext.Engagements.AddAsync(engagement, cancellationToken);

		var members = await keycloakOrganizationService
			.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);

		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var notification = Notification.Create(
				new UserId(organizer.UserId),
				NotificationKind.EngagementCreated,
				engagement.Id.Value);

			await dbContext.Notifications.AddAsync(notification, cancellationToken);
		}

		return engagement;
	}
}
