using Domain.Achievements;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Common.Persistence;

public interface IApplicationDbContext
{
	IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> VolunteerOpportunities { get; }

	IAggregateRepository<Organization, OrganizationId> Organizations { get; }

	IAggregateRepository<Engagement, EngagementId> Engagements { get; }

	IAggregateRepository<Notification, NotificationId> Notifications { get; }

	IAggregateRepository<User, UserId> Users { get; }

	IAggregateRepository<Achievement, AchievementId> Achievements { get; }

	Task<bool> HasAchievementAsync(
		UserId userId,
		AchievementType type,
		CancellationToken cancellationToken = default);

	ValueTask<List<Notification>> GetUnreadNotificationsForRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default);
}
