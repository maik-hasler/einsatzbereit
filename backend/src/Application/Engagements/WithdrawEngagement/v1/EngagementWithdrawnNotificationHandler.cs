using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.WithdrawEngagement.v1;

// Consumer of EngagementWithdrawnDomainEvent (einsatzbereit#1382):
// WithdrawEngagementCommandHandler only flips Status and raises the event;
// the organizer Keycloak lookup, in-app notifications, and withdrawal emails
// - previously sent synchronously inside the command's own DB transaction -
// happen here, dispatched by OutboxProcessorJob after that transaction has
// already committed.
//
// Publisher.Publish() resolves this handler from its own fresh child scope
// (see Application/Common/Messaging/Publisher.cs), not the scope
// OutboxProcessorJob itself is running in - so the IApplicationDbContext
// injected here is a *different* DbContext instance than the one
// OutboxProcessorJob.ProcessBatchAsync later calls SaveChangesAsync on.
// Nothing else persists this handler's writes (the new Notification rows),
// so it must call SaveChangesAsync itself via IUnitOfWork.
internal sealed class EngagementWithdrawnNotificationHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
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
				"Skipping withdrawal notification for engagement {EngagementId}: opportunity {OpportunityId} no longer exists",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		var volunteer = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);
		var volunteerName = volunteer.FirstName ?? volunteer.Username;

		var members = await keycloakOrganizationService.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);
		var organizers = members.Where(m => m.IsOrganisator).ToList();

		var organizerIds = organizers
			.Select(m => UserId.Create(m.UserId).GetValueOrThrow())
			.ToList();
		var organizerUsersById = (await dbContext.GetOrCreateUsersAsync(organizerIds, cancellationToken))
			.ToDictionary(u => u.Id);

		foreach (var organizer in organizers)
		{
			var organizerId = UserId.Create(organizer.UserId).GetValueOrThrow();
			var inAppNotification = Notification.Create(
				organizerId,
				NotificationKind.EngagementWithdrawn,
				notification.EngagementId.Value);

			await dbContext.Notifications.AddAsync(inAppNotification, cancellationToken);

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

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
