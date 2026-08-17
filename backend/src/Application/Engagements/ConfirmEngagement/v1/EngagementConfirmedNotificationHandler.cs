using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.ConfirmEngagement.v1;

// Consumer of EngagementConfirmedDomainEvent (#1150): ConfirmEngagementCommandHandler
// only flips Status and raises the event; the volunteer's confirmation email happens
// here, dispatched by OutboxProcessorJob like every other domain event, so a transient
// email failure is retried on the next poll cycle instead of having already been sent
// before the triggering command's transaction could even commit.
internal sealed class EngagementConfirmedNotificationHandler(
	IApplicationDbContext dbContext,
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
				"Skipping confirmation email for engagement {EngagementId}: opportunity {OpportunityId} no longer exists",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.VolunteerId], cancellationToken))[0];
		if (!volunteerUser.IsSubscribedTo(EmailNotificationType.EngagementConfirmed))
			return;

		var volunteer = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

		var content = emailTemplateRenderer.Render(
			EmailTemplateKind.EngagementConfirmed,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
				["OpportunityTitle"] = opportunity.TitleDe,
			});

		var unsubscribeUrl = unsubscribeLinkBuilder.Build(
			notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementConfirmed);

		await emailService.SendAsync(
			volunteer.Email,
			content.Subject,
			EmailFooter.Append(emailTemplateRenderer, volunteerLanguage, content.Body, unsubscribeUrl),
			notification.EngagementId.Value.ToString(),
			cancellationToken);
	}
}
