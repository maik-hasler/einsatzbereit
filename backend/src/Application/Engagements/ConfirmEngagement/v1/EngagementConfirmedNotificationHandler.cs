using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.ConfirmEngagement.v1;

// Consumer of EngagementConfirmedDomainEvent (einsatzbereit#1382):
// ConfirmEngagementCommandHandler only flips Status and raises the event; the
// volunteer's in-app notification, Keycloak lookup, and confirmation email -
// previously sent synchronously inside the command's own DB transaction -
// happen here, dispatched by OutboxProcessorJob after that transaction has
// already committed.
//
// Publisher.Publish() resolves this handler from its own fresh child scope
// (see Application/Common/Messaging/Publisher.cs), not the scope
// OutboxProcessorJob itself is running in - so the IApplicationDbContext
// injected here is a *different* DbContext instance than the one
// OutboxProcessorJob.ProcessBatchAsync later calls SaveChangesAsync on.
// Nothing else persists this handler's writes (the new Notification row), so
// it must call SaveChangesAsync itself via IUnitOfWork.
internal sealed class EngagementConfirmedNotificationHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementConfirmedNotificationHandler> logger)
	: INotificationHandler<EngagementConfirmedDomainEvent>
{
	public async Task Handle(
		EngagementConfirmedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken);

		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping confirmation notification for engagement {EngagementId}: opportunity {OpportunityId} no longer exists",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		var inAppNotification = Notification.Create(
			notification.VolunteerId,
			NotificationKind.EngagementConfirmed,
			notification.EngagementId.Value);

		await dbContext.Notifications.AddAsync(inAppNotification, cancellationToken);

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.VolunteerId], cancellationToken))[0];

		if (volunteerUser.IsSubscribedTo(EmailNotificationType.EngagementConfirmed))
		{
			var volunteer = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);
			var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

			var content = emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementConfirmed,
				volunteerLanguage,
				new Dictionary<string, string>
				{
					["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
					["OpportunityTitle"] = opportunity.Title,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementConfirmed);

			await emailService.SendAsync(
				volunteer.Email,
				content.Subject,
				EmailFooter.Append(content.Body, unsubscribeUrl),
				cancellationToken);
		}

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
