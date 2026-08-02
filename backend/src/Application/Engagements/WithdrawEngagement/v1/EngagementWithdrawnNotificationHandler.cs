using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.WithdrawEngagement.v1;

// Consumer of EngagementWithdrawnDomainEvent (#1150): WithdrawEngagementCommandHandler
// (and DeleteMyAccountCommandHandler, which withdraws non-terminal engagements before
// anonymizing - #1140) only flips Status and raises the event; the organizers' email
// happens here, dispatched by OutboxProcessorJob like every other domain event, so a
// transient email failure is retried on the next poll cycle instead of having already
// been sent before the triggering command's transaction could even commit.
internal sealed class EngagementWithdrawnNotificationHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementWithdrawnNotificationHandler> logger)
	: INotificationHandler<EngagementWithdrawnDomainEvent>
{
	public async Task Handle(
		EngagementWithdrawnDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken);
		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping withdrawal email for engagement {EngagementId}: opportunity {OpportunityId} no longer exists",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		// DeleteMyAccountCommandHandler withdraws non-terminal engagements and raises
		// UserAccountDeletedDomainEvent in the same commit (#1140/#1141) - both are
		// dispatched from the same outbox batch with no ordering guarantee between
		// them, so this can legitimately run after the volunteer's Keycloak identity
		// is already deleted. Retrying would never resolve that, so skip the
		// notification rather than dead-lettering forever.
		KeycloakUserProfile volunteer;
		try
		{
			volunteer = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Skipping withdrawal email for engagement {EngagementId}: volunteer {VolunteerId} could not be looked up in Keycloak",
				notification.EngagementId.Value,
				notification.VolunteerId.Value);
			return;
		}

		var volunteerName = volunteer.FirstName ?? volunteer.Username;

		var members = await keycloakOrganizationService.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);

		var organizerIds = members
			.Where(m => m.IsOrganisator)
			.Select(m => UserId.Create(m.UserId).GetValueOrThrow())
			.ToList();
		var organizerUsersById = (await dbContext.GetOrCreateUsersAsync(organizerIds, cancellationToken))
			.ToDictionary(u => u.Id);

		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var organizerId = UserId.Create(organizer.UserId).GetValueOrThrow();
			var organizerUser = organizerUsersById[organizerId];

			if (!organizerUser.IsSubscribedTo(EmailNotificationType.Withdrawal))
				continue;

			var organizerName = organizer.FirstName ?? organizer.Username;
			var organizerLanguage = SupportedLanguages.Resolve(organizerUser.PreferredLanguage);

			var content = emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
				organizerLanguage,
				new Dictionary<string, string>
				{
					["OrganizerName"] = organizerName,
					["VolunteerName"] = volunteerName,
					["OpportunityTitle"] = opportunity.Title,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				organizerId, organizerUser.UnsubscribeToken, EmailNotificationType.Withdrawal);

			await emailService.SendAsync(
				organizer.Email,
				content.Subject,
				EmailFooter.Append(content.Body, unsubscribeUrl),
				cancellationToken);
		}
	}
}
