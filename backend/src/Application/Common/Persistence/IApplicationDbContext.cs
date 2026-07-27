using Domain.Achievements;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Reports;
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

	IAggregateRepository<OrganizationInvitation, OrganizationInvitationId> OrganizationInvitations { get; }

	IAggregateRepository<OrganizationMembership, OrganizationMembershipId> OrganizationMemberships { get; }

	IAggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId> OrganizationDashboardLayouts { get; }

	IAggregateRepository<Report, ReportId> Reports { get; }

	Task<bool> IsOrganizerAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<int> CountOrganizersAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<OrganizationDashboardLayout?> GetDashboardLayoutAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default);

	Task RemoveMembershipAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default);

	Task RemoveMembershipsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task RemoveDashboardLayoutsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task RemoveMembershipsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task RemoveDashboardLayoutsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task DeleteInvitationsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<List<Organization>> GetOrganizerOrganizationsAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<bool> HasPendingInvitationAsync(
		OrganizationId organizationId,
		UserId inviteeId,
		CancellationToken cancellationToken = default);

	Task<List<OrganizationInvitation>> GetInvitationsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<List<OrganizationInvitation>> GetPendingInvitationsForUserAsync(
		UserId inviteeId,
		CancellationToken cancellationToken = default);

	Task<bool> HasAchievementAsync(
		UserId userId,
		string badgeName,
		CancellationToken cancellationToken = default);

	Task DeleteAchievementsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<bool> HasEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	IAggregateRepository<UserStreak, UserStreakId> UserStreaks { get; }

	Task<UserStreak?> GetUserStreakAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task DeleteUserStreakAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	ValueTask<List<Notification>> GetUnreadNotificationsForRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default);

	Task DeleteNotificationsForRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default);

	Task<List<Engagement>> GetEngagementsForVolunteerTrackingAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default);

	Task<int> CountConfirmedEngagementsForVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default);

	Task<int> CountActiveEngagementsForTimeSlotAsync(
		TimeSlotId timeSlotId,
		CancellationToken cancellationToken = default);

	Task<List<Engagement>> GetActiveEngagementsForOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	Task<List<VolunteerOpportunity>> GetBlockingOpportunitiesForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<Engagement?> GetTerminalEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	Task<bool> CanConnectAsync(
		CancellationToken cancellationToken = default);
}
