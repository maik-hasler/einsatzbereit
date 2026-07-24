using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Notifications;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Notifications;

internal static class OpportunityNotificationHelper
{
	private static readonly string[] ActiveStatuses = ["Pending", "Confirmed"];

	/// <summary>
	/// Creates a notification of the given kind for every distinct volunteer who
	/// has an active (pending or confirmed) engagement on the opportunity, or -
	/// when <paramref name="timeSlotId"/> is given - only those engaged on that
	/// specific time slot. The opportunity id is used as the related entity id.
	/// </summary>
	public static async Task NotifyActiveVolunteersAsync(
		IApplicationDbContext dbContext,
		IEngagementReadRepository engagementReadRepository,
		VolunteerOpportunityId opportunityId,
		NotificationKind kind,
		CancellationToken cancellationToken,
		TimeSlotId? timeSlotId = null)
	{
		var engagements = await engagementReadRepository.GetByOpportunityAsync(
			opportunityId, cancellationToken);

		var volunteerIds = engagements
			.Where(e => ActiveStatuses.Contains(e.Status) && e.VolunteerId is not null)
			.Where(e => timeSlotId is null || e.TimeSlotId == timeSlotId.Value.Value)
			.Select(e => e.VolunteerId!.Value)
			.Distinct();

		foreach (var volunteerId in volunteerIds)
		{
			var notification = Notification.Create(
				UserId.Create(volunteerId).GetValueOrThrow(),
				kind,
				opportunityId.Value);

			await dbContext.Notifications.AddAsync(notification, cancellationToken);
		}
	}
}
