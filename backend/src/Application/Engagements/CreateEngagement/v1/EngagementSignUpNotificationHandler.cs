using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CreateEngagement.v1;

// Consumer of EngagementCreatedDomainEvent and EngagementReactivatedDomainEvent (#1150):
// CreateEngagementCommandHandler only creates/reactivates the engagement and raises the
// event; the volunteer's own confirmation email and the organizers' new-sign-up email
// happen here, dispatched by OutboxProcessorJob like every other domain event, so a
// transient email failure is retried on the next poll cycle instead of having already
// been sent before the triggering command's transaction could even commit. Both events
// carry identical data and produce the identical notification, since a reactivated
// engagement (a volunteer re-signing-up after a withdrawal/cancellation) reads exactly
// like a fresh sign-up to everyone involved.
internal sealed class EngagementSignUpNotificationHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementSignUpNotificationHandler> logger)
	: INotificationHandler<EngagementCreatedDomainEvent>,
		INotificationHandler<EngagementReactivatedDomainEvent>
{
	public Task Handle(EngagementCreatedDomainEvent notification, CancellationToken cancellationToken) =>
		NotifyAsync(notification.EngagementId, notification.VolunteerId, notification.OpportunityId, cancellationToken);

	public Task Handle(EngagementReactivatedDomainEvent notification, CancellationToken cancellationToken) =>
		NotifyAsync(notification.EngagementId, notification.VolunteerId, notification.OpportunityId, cancellationToken);

	private async Task NotifyAsync(
		EngagementId engagementId,
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(opportunityId, cancellationToken);
		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping sign-up notification for engagement {EngagementId}: opportunity {OpportunityId} no longer exists",
				engagementId.Value,
				opportunityId.Value);
			return;
		}

		var engagement = await dbContext.Engagements.FindAsync(engagementId, cancellationToken);
		if (engagement is null)
		{
			logger.LogWarning(
				"Skipping sign-up notification: engagement {EngagementId} no longer exists",
				engagementId.Value);
			return;
		}

		var isSlotSignUp = engagement.TimeSlotId is not null;

		var volunteer = await keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken);
		var volunteerName = volunteer.FirstName ?? volunteer.Username;

		var volunteerUser = await dbContext.Users.FindAsync(volunteerId, cancellationToken);
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser?.PreferredLanguage);

		var volunteerContent = emailTemplateRenderer.Render(
			isSlotSignUp ? EmailTemplateKind.EngagementWaitlisted : EmailTemplateKind.EngagementRequestReceived,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteerName,
				["OpportunityTitle"] = opportunity.Title,
			});

		// Never gated by preference (#1055): this is the direct response to the
		// volunteer's own just-submitted action, not a repeatable notification about
		// someone else's activity - equivalent to an order receipt, which platforms
		// conventionally don't let users opt out of.
		await emailService.SendAsync(volunteer.Email, volunteerContent.Subject, volunteerContent.Body, cancellationToken);

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

			if (!organizerUser.IsSubscribedTo(EmailNotificationType.NewSignUp))
				continue;

			var organizerName = organizer.FirstName ?? organizer.Username;
			var organizerLanguage = SupportedLanguages.Resolve(organizerUser.PreferredLanguage);

			var organizerContent = emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementSignupNotifyOrganizer,
				organizerLanguage,
				new Dictionary<string, string>
				{
					["OrganizerName"] = organizerName,
					["VolunteerName"] = volunteerName,
					["OpportunityTitle"] = opportunity.Title,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				organizerId, organizerUser.UnsubscribeToken, EmailNotificationType.NewSignUp);

			await emailService.SendAsync(
				organizer.Email,
				organizerContent.Subject,
				EmailFooter.Append(organizerContent.Body, unsubscribeUrl),
				cancellationToken);
		}
	}
}
