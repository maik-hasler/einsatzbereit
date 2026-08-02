using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CreateEngagement.v1;

// Consumer of EngagementCreatedDomainEvent and EngagementReactivatedDomainEvent
// (einsatzbereit#1382): CreateEngagementCommandHandler only validates and
// creates/reactivates the Engagement; the organizer Keycloak lookup, in-app
// notifications, and volunteer/organizer emails - previously done inline
// inside the command's own DB transaction (2-3 blocking Keycloak round trips
// plus a new SmtpClient per recipient, all while row locks were held) -
// happen here instead, dispatched by OutboxProcessorJob after that
// transaction has already committed.
//
// Publisher.Publish() resolves this handler from its own fresh child scope
// (see Application/Common/Messaging/Publisher.cs), not the scope
// OutboxProcessorJob itself is running in - so the IApplicationDbContext
// injected here is a *different* DbContext instance than the one
// OutboxProcessorJob.ProcessBatchAsync later calls SaveChangesAsync on.
// Nothing else persists this handler's writes (the new Notification rows),
// so it must call SaveChangesAsync itself via IUnitOfWork.
internal sealed class EngagementSignUpNotificationHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
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
		var engagement = await dbContext.Engagements.FindAsync(engagementId, cancellationToken);

		if (opportunity is null || engagement is null)
		{
			// Deleted/withdrawn between the sign-up committing and the outbox
			// dispatching this event - nothing left to notify about, and
			// retrying would never resolve.
			logger.LogWarning(
				"Skipping sign-up notification for engagement {EngagementId}: opportunity or engagement no longer exists",
				engagementId.Value);
			return;
		}

		var isSlotSignUp = engagement.TimeSlotId is not null;

		var members = await keycloakOrganizationService.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);
		var organizers = members.Where(m => m.IsOrganisator).ToList();

		foreach (var organizer in organizers)
		{
			var organizerNotification = Notification.Create(
				UserId.Create(organizer.UserId).GetValueOrThrow(),
				NotificationKind.EngagementCreated,
				engagementId.Value);

			await dbContext.Notifications.AddAsync(organizerNotification, cancellationToken);
		}

		var volunteer = await keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken);
		var volunteerName = volunteer.FirstName ?? volunteer.Username;

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([volunteerId], cancellationToken))[0];
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

		var volunteerContent = emailTemplateRenderer.Render(
			isSlotSignUp ? EmailTemplateKind.EngagementWaitlisted : EmailTemplateKind.EngagementRequestReceived,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteerName,
				["OpportunityTitle"] = opportunity.Title,
			});

		// Never gated by preference (#1055): this is the direct response to the
		// volunteer's own just-submitted action, not a repeatable notification
		// about someone else's activity - equivalent to an order receipt, which
		// platforms conventionally don't let users opt out of.
		await emailService.SendAsync(volunteer.Email, volunteerContent.Subject, volunteerContent.Body, cancellationToken);

		var organizerIds = organizers
			.Select(m => UserId.Create(m.UserId).GetValueOrThrow())
			.ToList();
		var organizerUsersById = (await dbContext.GetOrCreateUsersAsync(organizerIds, cancellationToken))
			.ToDictionary(u => u.Id);

		foreach (var organizer in organizers)
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

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
