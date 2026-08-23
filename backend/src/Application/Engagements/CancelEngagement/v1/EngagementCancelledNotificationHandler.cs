using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CancelEngagement.v1;

internal sealed class EngagementCancelledNotificationHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementCancelledNotificationHandler> logger)
	: INotificationHandler<EngagementCancelledDomainEvent>
{
	public async Task Handle(
		EngagementCancelledDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunityTitle = notification.OpportunityTitle
			?? (await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken))?.TitleDe;

		if (opportunityTitle is null)
		{
			logger.LogWarning(
				"Skipping cancellation email for engagement {EngagementId}: opportunity title unavailable for {OpportunityId}",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.VolunteerId], cancellationToken))[0];
		if (!volunteerUser.IsSubscribedTo(EmailNotificationType.EngagementCancelled))
			return;

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
				["OpportunityTitle"] = opportunityTitle,
				["ReasonBlock"] = reasonBlock,
			});

		var unsubscribeUrl = unsubscribeLinkBuilder.Build(
			notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementCancelled);

		await emailService.SendAsync(
			volunteer.Email,
			content.Subject,
			EmailFooter.Append(emailTemplateRenderer, volunteerLanguage, content.Body, unsubscribeUrl),
			notification.EngagementId.Value.ToString(),
			cancellationToken);
	}
}
