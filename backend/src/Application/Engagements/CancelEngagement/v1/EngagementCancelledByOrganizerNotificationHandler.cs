using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CancelEngagement.v1;

// Consumer of EngagementCancelledByOrganizerDomainEvent (einsatzbereit#1382):
// CancelEngagementCommandHandler only cancels the Engagement and raises the
// event (via Cancel(reason, notifyVolunteer: true) - distinct from the plain
// EngagementCancelledDomainEvent that cascade cancellations also raise, so
// this handler never double-notifies a cascade-cancelled engagement); the
// volunteer's in-app notification, Keycloak lookup, and cancellation email -
// previously sent synchronously inside the command's own DB transaction via
// EngagementCancellationHelper - happen here, dispatched by OutboxProcessorJob
// after that transaction has already committed.
//
// Publisher.Publish() resolves this handler from its own fresh child scope
// (see Application/Common/Messaging/Publisher.cs), not the scope
// OutboxProcessorJob itself is running in - so the IApplicationDbContext
// injected here is a *different* DbContext instance than the one
// OutboxProcessorJob.ProcessBatchAsync later calls SaveChangesAsync on.
// Nothing else persists this handler's writes (the new Notification row), so
// it must call SaveChangesAsync itself via IUnitOfWork.
internal sealed class EngagementCancelledByOrganizerNotificationHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementCancelledByOrganizerNotificationHandler> logger)
	: INotificationHandler<EngagementCancelledByOrganizerDomainEvent>
{
	public async Task Handle(
		EngagementCancelledByOrganizerDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken);

		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping cancellation notification for engagement {EngagementId}: opportunity {OpportunityId} no longer exists",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		var inAppNotification = Notification.Create(
			notification.VolunteerId,
			NotificationKind.EngagementCancelled,
			notification.EngagementId.Value);

		await dbContext.Notifications.AddAsync(inAppNotification, cancellationToken);

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.VolunteerId], cancellationToken))[0];

		if (volunteerUser.IsSubscribedTo(EmailNotificationType.EngagementCancelled))
		{
			var volunteer = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);
			var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

			var reasonBlock = string.IsNullOrWhiteSpace(notification.Reason)
				? string.Empty
				: emailTemplateRenderer.Render(
					EmailTemplateKind.EngagementCancelledReasonSuffix,
					volunteerLanguage,
					new Dictionary<string, string> { ["Reason"] = notification.Reason }).Body;

			var content = emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementCancelled,
				volunteerLanguage,
				new Dictionary<string, string>
				{
					["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
					["OpportunityTitle"] = opportunity.Title,
					["ReasonBlock"] = reasonBlock,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementCancelled);

			await emailService.SendAsync(
				volunteer.Email,
				content.Subject,
				EmailFooter.Append(content.Body, unsubscribeUrl),
				cancellationToken);
		}

		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
