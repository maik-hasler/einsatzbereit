using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Notifications;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Notifications;

internal static class OpportunityNotificationHelper
{
	/// <summary>
	/// Creates a notification of the given kind for every distinct volunteer who
	/// has an active (pending or confirmed) engagement on the opportunity, or -
	/// when <paramref name="timeSlotId"/> is given - only those engaged on that
	/// specific time slot. The opportunity id is used as the related entity id.
	/// When <paramref name="keycloakUserService"/>, <paramref name="emailService"/>
	/// and <paramref name="opportunityTitle"/> are all supplied, also emails every
	/// notified volunteer in a single batch (einsatzbereit#1057) - callers that
	/// already email affected volunteers through another path (e.g. an engagement
	/// cancellation) should omit these so the volunteer isn't emailed twice.
	/// </summary>
	public static async Task NotifyActiveVolunteersAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunityId opportunityId,
		NotificationKind kind,
		CancellationToken cancellationToken,
		TimeSlotId? timeSlotId = null,
		IKeycloakUserService? keycloakUserService = null,
		IEmailService? emailService = null,
		string? opportunityTitle = null)
	{
		var volunteerIds = await engagementReadRepository.GetActiveVolunteerIdsByOpportunityAsync(
			opportunityId, timeSlotId, cancellationToken);

		foreach (var volunteerId in volunteerIds)
		{
			var notification = Notification.Create(
				UserId.Create(volunteerId).GetValueOrThrow(),
				kind,
				opportunityId.Value);

			await dbContext.Notifications.AddAsync(notification, cancellationToken);
		}

		if (keycloakUserService is null || emailService is null || volunteerIds.Count == 0)
			return;

		var messages = new List<EmailMessage>(volunteerIds.Count);
		foreach (var volunteerId in volunteerIds)
		{
			var volunteer = await keycloakUserService.GetUserAsync(volunteerId, cancellationToken);
			messages.Add(new EmailMessage(
				volunteer.Email,
				$"An opportunity you signed up for has changed: \"{opportunityTitle}\"",
				$"Hello {volunteer.FirstName ?? volunteer.Username},\n\n" +
				$"The details for \"{opportunityTitle}\" have changed. Please check the app for the latest information.\n\nEinsatzbereit"));
		}

		await emailService.SendBatchAsync(messages, cancellationToken);
	}
}
