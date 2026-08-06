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

// Consumer of EngagementReminderDueDomainEvent (#1392): EngagementReminderJob only
// detects which engagements are due for a 24h reminder and atomically claims + queues
// them into the outbox (Infrastructure/BackgroundJobs/EngagementReminderJob.cs); actual
// delivery happens here, dispatched by OutboxProcessorJob like every other domain event,
// so a transient failure is retried on the next poll cycle instead of being lost.
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
			// The opportunity/time slot this reminder was queued for no longer exists
			// (deleted between claim and dispatch) - nothing to remind about, and
			// retrying would never resolve, so this is treated as handled rather than
			// re-thrown for the outbox to retry forever.
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
				["OpportunityTitle"] = opportunity.Title,
				["StartFormatted"] = startFormatted,
			});

		var unsubscribeUrl = unsubscribeLinkBuilder.Build(
			notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementReminder);

		var subject = content.Subject;
		var body = EmailFooter.Append(emailTemplateRenderer, language, content.Body, unsubscribeUrl);

		// SendBatchAsync with a single message (rather than SendAsync) so a failed send
		// is observable as a bool - SendAsync never throws and never reports outcome,
		// which would make it impossible to know whether to let the outbox retry.
		var results = await emailService.SendBatchAsync(
			[new EmailMessage(user.Email, subject, body, notification.EngagementId.Value.ToString())], cancellationToken);
		if (!results[0])
			throw new InvalidOperationException(
				$"Failed to send 24h reminder email for engagement {notification.EngagementId.Value}");

		logger.LogInformation(
			"Sent 24h reminder for engagement {EngagementId}",
			notification.EngagementId.Value);
	}

	// Mirrors the frontend's own locale mapping (frontend/src/lib/format.ts:
	// "de" -> "de-DE", else "en-GB") so reminder emails read naturally in
	// either language instead of leaking an English day/month name.
	//
	// No per-opportunity timezone is stored (see ConfirmEngagementCommandHandler's
	// own ResolveTimeZone), so - like every other server-side fallback in this
	// codebase - this defaults to Europe/Berlin rather than .ToLocalTime(), which
	// would resolve against the container's clock (UTC, since no TZ is set in the
	// API's Dockerfile) and announce the wrong hour to the volunteer.
	private static string FormatStart(DateTimeOffset startDateTime, string language)
	{
		var berlinTime = TimeZoneInfo.ConvertTime(startDateTime, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
		var culture = CultureInfo.GetCultureInfo(language == "de" ? "de-DE" : "en-GB");
		var pattern = language == "de" ? "dddd, d. MMMM yyyy 'um' HH:mm" : "dddd, d. MMMM yyyy 'at' HH:mm";
		return berlinTime.ToString(pattern, culture);
	}
}
