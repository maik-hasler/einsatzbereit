using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
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
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer)
	: ICommandHandler<WithdrawEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		WithdrawEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		if (engagement.VolunteerId!.Value.Value != request.VolunteerId)
			throw new ResultFailureException(Error.Forbidden("Engagement.NotOwner", "Only the volunteer who created this engagement can withdraw it."));

		engagement.Withdraw().ThrowIfFailure();

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
				var organizerId = UserId.Create(organizer.UserId).GetValueOrThrow();
				var notification = Notification.Create(
					organizerId,
					NotificationKind.EngagementWithdrawn,
					engagement.Id.Value);

				await dbContext.Notifications.AddAsync(notification, cancellationToken);

				var organizerName = organizer.FirstName ?? organizer.Username;
				var organizerUser = await dbContext.Users.FindAsync(organizerId, cancellationToken);
				var organizerLanguage = SupportedLanguages.Resolve(organizerUser?.PreferredLanguage);

				var content = emailTemplateRenderer.Render(
					EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
					organizerLanguage,
					new Dictionary<string, string>
					{
						["OrganizerName"] = organizerName,
						["VolunteerName"] = volunteerName,
						["OpportunityTitle"] = opportunity.Title,
					});

				await emailService.SendAsync(organizer.Email, content.Subject, content.Body, cancellationToken);
			}
		}

		return engagement;
	}
}
