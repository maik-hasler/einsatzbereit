using Application.Common.Exceptions;
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
	}
}
