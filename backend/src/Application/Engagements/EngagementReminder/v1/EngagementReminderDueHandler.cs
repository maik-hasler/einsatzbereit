using System.Globalization;
using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.EngagementReminder.v1;

internal sealed class EngagementReminderDueHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementReminderDueHandler> logger)
	: INotificationHandler<EngagementReminderDueDomainEvent>
{
	public async Task Handle(
		EngagementReminderDueDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken);
		var timeSlot = opportunity?.TimeSlots.FirstOrDefault(ts => ts.Id == notification.TimeSlotId);

		if (opportunity is null || timeSlot is null)
		{
			logger.LogWarning(
				"Skipping reminder for engagement {EngagementId}: opportunity {OpportunityId} or time slot {TimeSlotId} no longer exists",
				notification.EngagementId.Value,
				notification.OpportunityId.Value,
				notification.TimeSlotId.Value);
			return;
		}

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.VolunteerId], cancellationToken))[0];

		if (!volunteerUser.IsSubscribedTo(EmailNotificationType.EngagementReminder))
		{
			logger.LogInformation(
				"Skipping 24h reminder for engagement {EngagementId}: volunteer opted out of reminder emails",
				notification.EngagementId.Value);
			return;
		}

		var user = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);

		var displayName = $"{user.FirstName} {user.LastName}".Trim();
		if (string.IsNullOrEmpty(displayName))
			displayName = user.Username;

		var language = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

		var startFormatted = FormatStart(timeSlot.StartDateTime, language);

		var content = emailTemplateRenderer.Render(
			EmailTemplateKind.EngagementReminder,
			language,
			new Dictionary<string, string>
			{
				["DisplayName"] = displayName,
				["OpportunityTitle"] = opportunity.TitleDe,
				["StartFormatted"] = startFormatted,
			});

		var unsubscribeUrl = unsubscribeLinkBuilder.Build(
			notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementReminder);

		var subject = content.Subject;
		var body = EmailFooter.Append(emailTemplateRenderer, language, content.Body, unsubscribeUrl);

		var results = await emailService.SendBatchAsync(
			[new EmailMessage(user.Email, subject, body, notification.EngagementId.Value.ToString())], cancellationToken);
		if (!results[0])
			throw new InvalidOperationException(
				$"Failed to send 24h reminder email for engagement {notification.EngagementId.Value}");

		logger.LogInformation(
			"Sent 24h reminder for engagement {EngagementId}",
			notification.EngagementId.Value);
	}

	private static string FormatStart(DateTimeOffset startDateTime, string language)
	{
		var berlinTime = TimeZoneInfo.ConvertTime(startDateTime, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
		var culture = CultureInfo.GetCultureInfo(language == "de" ? "de-DE" : "en-GB");
		var pattern = language == "de" ? "dddd, d. MMMM yyyy 'um' HH:mm" : "dddd, d. MMMM yyyy 'at' HH:mm";
		return berlinTime.ToString(pattern, culture);
	}
}
