using Application.Organizations;
using Domain.Achievements;
using Domain.AuditLogs;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Reports;
using Domain.SearchAlerts;
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

	IAggregateRepository<AuditLog, AuditLogId> AuditLogs { get; }

	IAggregateRepository<SearchAlert, SearchAlertId> SearchAlerts { get; }

	Task<SearchAlert?> GetSearchAlertForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task DeleteSearchAlertForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task DeleteReportsForReporterAsync(
		UserId reporterId,
		CancellationToken cancellationToken = default);

	Task<List<VolunteerOpportunity>> GetVolunteerOpportunitiesByIdsAsync(
		IReadOnlyCollection<VolunteerOpportunityId> opportunityIds,
		CancellationToken cancellationToken = default);

	Task<bool> IsOrganizerAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<bool> IsMemberAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<int> CountOrganizersAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<HashSet<Guid>> GetOrganizerUserIdsAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	// Every member's role, sourced entirely from organization_membership - unlike
	// GetOrganizerUserIdsAsync (organizers only), this covers plain Members too.
	// GetOrganizationDetailsQueryHandler falls back to this when Keycloak's member
	// lookup fails (#1709), so the org app shell can still render a roster (ids +
	// roles only, no Keycloak-sourced username/email/name) instead of 500ing.
	Task<Dictionary<Guid, OrganizationMemberRole>> GetMembershipRolesAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<OrganizationMembership?> GetMembershipAsync(
		OrganizationId organizationId,
		UserId userId,
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

	Task<List<Organization>> GetMemberOrganizationsAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<List<OrganizationMembershipSummary>> GetMembershipsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	// Atomically inserts the invitation only if no Pending invitation already
	// exists for (OrganizationId, InviteeId) - backed by a partial unique index
	// on exactly that predicate, so this can't lose a race the way a separate
	// existence check followed by an unconditional insert could (#1202).
	// Returns false (nothing inserted) when a Pending invitation already exists.
	Task<bool> TryCreateInvitationAsync(
		OrganizationInvitation invitation,
		CancellationToken cancellationToken = default);

	Task<Dictionary<Guid, string>> GetOrganizationNamesAsync(
		IReadOnlyCollection<OrganizationId> organizationIds,
		CancellationToken cancellationToken = default);

	Task<List<OrganizationInvitation>> GetInvitationsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<List<OrganizationInvitation>> GetPendingInvitationsForUserAsync(
		UserId inviteeId,
		CancellationToken cancellationToken = default);

	// Atomically awards the badge only if the user doesn't already have it, via a
	// single "INSERT ... ON CONFLICT (user_id, key) DO NOTHING" instead of a
	// separate existence check followed by a tracked insert - two concurrent
	// awards of the same badge (e.g. two engagements confirmed for the same
	// volunteer at once) would otherwise both pass the existence check and race
	// on the unique index, surfacing as a 500 that rolls back the whole
	// triggering command (#1205). Returns true if this call actually inserted
	// the row, false if it already existed.
	Task<bool> TryAwardAchievementAsync(
		Achievement achievement,
		CancellationToken cancellationToken = default);

	Task DeleteAchievementsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<bool> HasEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		TimeSlotId? timeSlotId,
		CancellationToken cancellationToken = default);

	IAggregateRepository<UserStreak, UserStreakId> UserStreaks { get; }

	Task<UserStreak?> GetUserStreakAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	// Atomic get-or-create (#1204): the naive "read, then Create+Add if null"
	// pattern used to race two concurrent first-touches for the same user (e.g.
	// ConfirmEngagement racing LoginStreakMiddleware's out-of-band RecordLogin) -
	// both would see no row, both insert, and the loser's whole SaveChangesAsync
	// died on ix_user_streak_user_id with a 500. This resolves the race with a
	// single "INSERT ... ON CONFLICT (user_id) DO NOTHING" instead, mirroring
	// GetOrCreateUserAsync below.
	Task<UserStreak> GetOrCreateUserStreakAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	Task<int> CountUserStreaksAsync(
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

	Task<int> DeleteReadNotificationsForRecipientAsync(
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

	// Takes a row lock on the time slot before counting active sign-ups, so a
	// second concurrent sign-up for the same slot blocks until the first one's
	// transaction commits instead of both reading the same stale count (#1142).
	Task LockTimeSlotForUpdateAsync(
		TimeSlotId timeSlotId,
		CancellationToken cancellationToken = default);

	Task<List<Engagement>> GetActiveEngagementsForOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	Task<List<Engagement>> GetActiveEngagementsForTimeSlotsAsync(
		IReadOnlyCollection<TimeSlotId> timeSlotIds,
		CancellationToken cancellationToken = default);

	Task<List<VolunteerOpportunity>> GetBlockingOpportunitiesForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<List<VolunteerOpportunity>> GetOpportunitiesForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<bool> HasOpenReportAsync(
		ReportTargetType targetType,
		Guid targetId,
		UserId reporterId,
		CancellationToken cancellationToken = default);

	Task<List<Report>> GetOpenReportsForTargetAsync(
		ReportTargetType targetType,
		Guid targetId,
		CancellationToken cancellationToken = default);

	Task<List<Report>> GetReportHistoryForTargetAsync(
		ReportTargetType targetType,
		Guid targetId,
		CancellationToken cancellationToken = default);

	Task<Organization?> FindOrganizationIncludingDeletedAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default);

	Task<VolunteerOpportunity?> FindVolunteerOpportunityIncludingDeletedAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default);

	Task<User?> FindUserIncludingDeletedAsync(
		UserId userId,
		CancellationToken cancellationToken = default);

	// Notification-preference/unsubscribe-token lookups (#1055) need a durable
	// User row for every email recipient, even one who has never opened their
	// profile page and so has no row yet - creates (but does not save) one for
	// each id missing from the table, mirroring the same lazy-create already
	// done inline in GetUserProfileQueryHandler/UpdateUserProfileCommandHandler.
	Task<List<User>> GetOrCreateUsersAsync(
		IReadOnlyCollection<UserId> userIds,
		CancellationToken cancellationToken = default);

	// Unlike GetOrCreateUsersAsync above, this commits its own insert immediately via
	// a single atomic "INSERT ... ON CONFLICT DO NOTHING" statement instead of relying
	// on the caller's SaveChangesAsync - so it's safe to call from a query handler with
	// no ambient transaction, where two concurrent first-time callers would otherwise
	// both try to insert the same Keycloak-UserId-keyed row and one would 500 (#1148).
	Task<User> GetOrCreateUserAsync(
		UserId userId,
		string? preferredLanguage,
		CancellationToken cancellationToken = default);

	Task<Engagement?> GetTerminalEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		TimeSlotId? timeSlotId,
		CancellationToken cancellationToken = default);

	Task<bool> CanConnectAsync(
		CancellationToken cancellationToken = default);
}
