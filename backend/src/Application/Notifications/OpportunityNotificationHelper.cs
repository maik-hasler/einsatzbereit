using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Notifications;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Notifications;

internal static class OpportunityNotificationHelper
{
	public static async Task NotifyActiveVolunteersAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunityId opportunityId,
		NotificationKind kind,
		CancellationToken cancellationToken,
		TimeSlotId? timeSlotId = null,
		IKeycloakUserService? keycloakUserService = null,
		IEmailService? emailService = null,
		IEmailTemplateRenderer? emailTemplateRenderer = null,
		string? opportunityTitle = null)
	{
		var volunteerIds = await engagementReadRepository.GetActiveVolunteerIdsByOpportunityAsync(
			opportunityId, timeSlotId, cancellationToken);

		foreach (var volunteerId in volunteerIds)
		{
			var notification = Notification.Create(
				UserId.Create(volunteerId).GetValueOrThrow(),
				kind,
				opportunityId.Value,
				opportunityTitle);

			await dbContext.Notifications.AddAsync(notification, cancellationToken);
		}

		if (keycloakUserService is null || emailService is null || emailTemplateRenderer is null || volunteerIds.Count == 0)
			return;

		var volunteerUserIds = volunteerIds.Select(id => UserId.Create(id).GetValueOrThrow()).ToList();
		var volunteerUsersById = (await dbContext.GetOrCreateUsersAsync(volunteerUserIds, cancellationToken))
			.ToDictionary(u => u.Id);

		var profileMap = await keycloakUserService.GetUserProfilesAsync(volunteerIds, cancellationToken);

		var messages = new List<EmailMessage>(volunteerIds.Count);
		foreach (var volunteerId in volunteerIds)
		{
			if (!profileMap.TryGetValue(volunteerId, out var volunteer))
				continue;

			var volunteerUser = volunteerUsersById[UserId.Create(volunteerId).GetValueOrThrow()];
			var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

			var content = emailTemplateRenderer.Render(
				EmailTemplateKind.OpportunityUpdated,
				volunteerLanguage,
				new Dictionary<string, string>
				{
					["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
					["OpportunityTitle"] = opportunityTitle ?? string.Empty,
				});

			messages.Add(new EmailMessage(volunteer.Email, content.Subject, content.Body, volunteerId.ToString()));
		}

		// Unlike EngagementOrganizerNotificationHelper, every live caller here that supplies
		// email params is a synchronous command handler running inside
		// TransactionPipelineBehavior's transaction, not a post-commit outbox-dispatched
		// handler - there's no retry infrastructure downstream to hand a failure to.
		// Throwing here would roll back the (unrelated, already-valid) opportunity edit and,
		// since a bad recipient address fails identically on every retry, could permanently
		// block the organizer from editing this opportunity at all (#2201). SmtpEmailService
		// already logs and records metrics for each failed send, so nothing is silently lost -
		// only the "block the caller" part is deliberately not propagated here.
		if (messages.Count > 0)
			await emailService.SendBatchAsync(messages, cancellationToken);
	}
}
