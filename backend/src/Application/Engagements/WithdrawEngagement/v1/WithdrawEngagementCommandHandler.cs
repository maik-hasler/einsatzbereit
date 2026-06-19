using Application.Common.Email;
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
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService)
	: ICommandHandler<WithdrawEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		WithdrawEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

		if (engagement.VolunteerId.Value != request.VolunteerId)
			throw new DomainException("Only the volunteer who created this engagement can withdraw it.");

		engagement.Withdraw();

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			engagement.OpportunityId, cancellationToken);

		if (opportunity is not null)
		{
			var volunteer = await keycloakUserService.GetUserAsync(request.VolunteerId, cancellationToken);
			var volunteerName = volunteer.FirstName ?? volunteer.Username;

			var members = await keycloakOrganizationService
				.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);

			foreach (var organizer in members.Where(m => m.IsOrganisator))
			{
				var notification = Notification.Create(
					new UserId(organizer.UserId),
					NotificationKind.EngagementWithdrawn,
					engagement.Id.Value);

				await dbContext.Notifications.AddAsync(notification, cancellationToken);

				var organizerName = organizer.FirstName ?? organizer.Username;
				await emailService.SendAsync(
					organizer.Email,
					$"{volunteerName} has withdrawn from \"{opportunity.Title}\"",
					$"Hi {organizerName},\n\n{volunteerName} has withdrawn from \"{opportunity.Title}\".\n\nEinsatzbereit",
					cancellationToken);
			}
		}

		return engagement;
	}
}
