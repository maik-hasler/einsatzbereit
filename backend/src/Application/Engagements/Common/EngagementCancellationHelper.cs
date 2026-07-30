using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;

namespace Application.Engagements.Common;

/// <summary>
/// Cancels an engagement and sends the same in-app notification + email that
/// <see cref="CancelEngagement.v1.CancelEngagementCommandHandler"/> sends for an
/// organizer-triggered cancellation - shared so engagements auto-cancelled by an
/// opportunity deletion notify the volunteer identically instead of only via the
/// opportunity-level notification (einsatzbereit#1057).
/// </summary>
internal static class EngagementCancellationHelper
{
	public static async Task CancelAndNotifyAsync(
		IApplicationDbContext dbContext,
		IKeycloakUserService keycloakUserService,
		IEmailService emailService,
		IEmailTemplateRenderer emailTemplateRenderer,
		Engagement engagement,
		string opportunityTitle,
		string? reason,
		CancellationToken cancellationToken)
	{
		engagement.Cancel(reason).ThrowIfFailure();

		var notification = Notification.Create(
			engagement.VolunteerId!.Value,
			NotificationKind.EngagementCancelled,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);
		var volunteer = await keycloakUserService.GetUserAsync(engagement.VolunteerId!.Value.Value, cancellationToken);
		var volunteerUser = await dbContext.Users.FindAsync(engagement.VolunteerId!.Value, cancellationToken);
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser?.PreferredLanguage);

		var reasonBlock = string.IsNullOrWhiteSpace(reason)
			? string.Empty
			: emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementCancelledReasonSuffix,
				volunteerLanguage,
				new Dictionary<string, string> { ["Reason"] = reason }).Body;

		var content = emailTemplateRenderer.Render(
			EmailTemplateKind.EngagementCancelled,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
				["OpportunityTitle"] = opportunityTitle,
				["ReasonBlock"] = reasonBlock,
			});

		await emailService.SendAsync(volunteer.Email, content.Subject, content.Body, cancellationToken);
	}
}
