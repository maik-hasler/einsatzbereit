using System.Reflection;
using Application.Common.Persistence;
using Application.Organizations;
using Domain.Achievements;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class ApplicationDbContext(
	DbContextOptions<ApplicationDbContext> options)
	: DbContext(options),
	IUnitOfWork,
	IApplicationDbContext
{
	public IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> VolunteerOpportunities
		=> new AggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>(
			Set<VolunteerOpportunity>(),
			Set<VolunteerOpportunity>().Include(vo => vo.TimeSlots),
			vo => vo.Id);

	internal IQueryable<VolunteerOpportunity> VolunteerOpportunitiesQuery => Set<VolunteerOpportunity>().AsNoTracking();

	internal IQueryable<TimeSlot> TimeSlotsQuery => Set<TimeSlot>().AsNoTracking();

	public IAggregateRepository<Organization, OrganizationId> Organizations
		=> new AggregateRepository<Organization, OrganizationId>(
			Set<Organization>(),
			Set<Organization>(),
			org => org.Id);

	internal IQueryable<Organization> OrganizationsQuery => Set<Organization>().AsNoTracking();

	public IAggregateRepository<Engagement, EngagementId> Engagements
		=> new AggregateRepository<Engagement, EngagementId>(
			Set<Engagement>(),
			Set<Engagement>(),
			e => e.Id);

	internal IQueryable<Engagement> EngagementsQuery => Set<Engagement>().AsNoTracking();

	public IAggregateRepository<Notification, NotificationId> Notifications
		=> new AggregateRepository<Notification, NotificationId>(
			Set<Notification>(),
			Set<Notification>(),
			n => n.Id);

	public IAggregateRepository<User, UserId> Users
		=> new AggregateRepository<User, UserId>(
			Set<User>(),
			Set<User>(),
			u => u.Id);

	internal IQueryable<User> UsersQuery => Set<User>().AsNoTracking();

	internal IQueryable<Notification> NotificationsQuery => Set<Notification>().AsNoTracking();

	public IAggregateRepository<Achievement, AchievementId> Achievements
		=> new AggregateRepository<Achievement, AchievementId>(
			Set<Achievement>(),
			Set<Achievement>(),
			a => a.Id);

	internal IQueryable<Achievement> AchievementsQuery => Set<Achievement>().AsNoTracking();

	public IAggregateRepository<OrganizationInvitation, OrganizationInvitationId> OrganizationInvitations
		=> new AggregateRepository<OrganizationInvitation, OrganizationInvitationId>(
			Set<OrganizationInvitation>(),
			Set<OrganizationInvitation>(),
			i => i.Id);

	internal IQueryable<OrganizationInvitation> OrganizationInvitationsQuery => Set<OrganizationInvitation>().AsNoTracking();

	public IAggregateRepository<OrganizationMembership, OrganizationMembershipId> OrganizationMemberships
		=> new AggregateRepository<OrganizationMembership, OrganizationMembershipId>(
			Set<OrganizationMembership>(),
			Set<OrganizationMembership>(),
			m => m.Id);

	public IAggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId> OrganizationDashboardLayouts
		=> new AggregateRepository<OrganizationDashboardLayout, OrganizationDashboardLayoutId>(
			Set<OrganizationDashboardLayout>(),
			Set<OrganizationDashboardLayout>(),
			l => l.Id);

	public IAggregateRepository<Report, ReportId> Reports
		=> new AggregateRepository<Report, ReportId>(
			Set<Report>(),
			Set<Report>(),
			r => r.Id);

	internal IQueryable<Report> ReportsQuery => Set<Report>().AsNoTracking();

	public async Task<bool> IsOrganizerAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.AnyAsync(m => m.OrganizationId == organizationId
				&& m.UserId == userId
				&& m.Role == OrganizationMemberRole.Organizer, cancellationToken);

	public async Task<int> CountOrganizersAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.CountAsync(m => m.OrganizationId == organizationId
				&& m.Role == OrganizationMemberRole.Organizer, cancellationToken);

	public async Task<HashSet<Guid>> GetOrganizerUserIdsAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default)
	{
		var userIds = await Set<OrganizationMembership>()
			.AsNoTracking()
			.Where(m => m.OrganizationId == organizationId && m.Role == OrganizationMemberRole.Organizer)
			.Select(m => m.UserId)
			.ToListAsync(cancellationToken);

		return userIds.Select(id => id.Value).ToHashSet();
	}

	public async Task<OrganizationMembership?> GetMembershipAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

	public async Task<OrganizationDashboardLayout?> GetDashboardLayoutAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationDashboardLayout>()
			.FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.UserId == userId, cancellationToken);

	public async Task RemoveMembershipAsync(
		OrganizationId organizationId,
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.Where(m => m.OrganizationId == organizationId && m.UserId == userId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task RemoveMembershipsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.Where(m => m.OrganizationId == organizationId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task RemoveDashboardLayoutsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationDashboardLayout>()
			.Where(l => l.OrganizationId == organizationId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task RemoveMembershipsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.Where(m => m.UserId == userId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task RemoveDashboardLayoutsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationDashboardLayout>()
			.Where(l => l.UserId == userId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task DeleteInvitationsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationInvitation>()
			.Where(i => i.InviteeId == userId || i.InvitedById == userId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task<List<Organization>> GetOrganizerOrganizationsAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationMembership>()
			.AsNoTracking()
			.Where(m => m.UserId == userId && m.Role == OrganizationMemberRole.Organizer)
			.Join(
				Set<Organization>().AsNoTracking(),
				m => m.OrganizationId,
				o => o.Id,
				(m, o) => o)
			.OrderBy(o => o.Name)
			.ToListAsync(cancellationToken);

	public async Task<List<OrganizationMembershipSummary>> GetMembershipsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default)
	{
		// Role.ToString() can't be pushed into the SQL projection below (Npgsql has
		// no translation for enum-to-string conversion mid-query) - project the raw
		// enum value instead and stringify it client-side after materializing.
		var raw = await Set<OrganizationMembership>()
			.AsNoTracking()
			.Where(m => m.UserId == userId)
			.Join(
				Set<Organization>().AsNoTracking(),
				m => m.OrganizationId,
				o => o.Id,
				(m, o) => new { OrganizationId = o.Id.Value, OrganizationName = o.Name, m.Role })
			.OrderBy(s => s.OrganizationName)
			.ToListAsync(cancellationToken);

		return raw
			.Select(s => new OrganizationMembershipSummary(s.OrganizationId, s.OrganizationName, s.Role.ToString()))
			.ToList();
	}

	public async Task<bool> HasAchievementAsync(
		UserId userId,
		string badgeName,
		CancellationToken cancellationToken = default) =>
		await Set<Achievement>()
			.AnyAsync(a => a.UserId == userId && a.Name == badgeName, cancellationToken);

	public async Task DeleteAchievementsForUserAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<Achievement>()
			.Where(a => a.UserId == userId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task<bool> HasEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		TimeSlotId? timeSlotId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.AnyAsync(e => e.VolunteerId == volunteerId
				&& e.OpportunityId == opportunityId
				&& e.TimeSlotId == timeSlotId
				&& e.Status != EngagementStatus.Withdrawn
				&& e.Status != EngagementStatus.Cancelled, cancellationToken);

	public IAggregateRepository<UserStreak, UserStreakId> UserStreaks
		=> new AggregateRepository<UserStreak, UserStreakId>(
			Set<UserStreak>(),
			Set<UserStreak>(),
			s => s.Id);

	internal IQueryable<UserStreak> UserStreaksQuery => Set<UserStreak>().AsNoTracking();

	public async Task<UserStreak?> GetUserStreakAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<UserStreak>()
			.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

	public async Task<int> CountUserStreaksAsync(
		CancellationToken cancellationToken = default) =>
		await Set<UserStreak>().CountAsync(cancellationToken);

	public async Task DeleteUserStreakAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<UserStreak>()
			.Where(s => s.UserId == userId)
			.ExecuteDeleteAsync(cancellationToken);

	public async ValueTask<List<Notification>> GetUnreadNotificationsForRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default) =>
		await Set<Notification>()
			.Where(n => n.RecipientId == recipientId && !n.IsRead)
			.ToListAsync(cancellationToken);

	public async Task DeleteNotificationsForRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default) =>
		await Set<Notification>()
			.Where(n => n.RecipientId == recipientId)
			.ExecuteDeleteAsync(cancellationToken);

	public async Task<List<Engagement>> GetEngagementsForVolunteerTrackingAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.Where(e => e.VolunteerId == volunteerId)
			.ToListAsync(cancellationToken);

	public async Task<int> CountConfirmedEngagementsForVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.CountAsync(e => e.VolunteerId == volunteerId && e.Status == EngagementStatus.Confirmed, cancellationToken);

	public async Task<int> CountActiveEngagementsForTimeSlotAsync(
		TimeSlotId timeSlotId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.CountAsync(e => e.TimeSlotId == timeSlotId
				&& (e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed),
				cancellationToken);

	public async Task LockTimeSlotForUpdateAsync(
		TimeSlotId timeSlotId,
		CancellationToken cancellationToken = default) =>
		await Set<TimeSlot>()
			.FromSqlInterpolated($@"
				SELECT id, end_date_time, max_participants, recurrence_count, recurrence_frequency, series_id, start_date_time, volunteer_opportunity_id
				FROM time_slot
				WHERE id = {timeSlotId.Value}
				FOR UPDATE")
			.ToListAsync(cancellationToken);

	public async Task<List<Engagement>> GetActiveEngagementsForOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.Where(e => e.OpportunityId == opportunityId
				&& (e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))
			.ToListAsync(cancellationToken);

	public async Task<List<Engagement>> GetActiveEngagementsForTimeSlotsAsync(
		IReadOnlyCollection<TimeSlotId> timeSlotIds,
		CancellationToken cancellationToken = default)
	{
		// Contains against a List<TimeSlotId?> (nullable-wrapped) translates
		// fine - unwrapping the nullable value object inside the query (e.g.
		// e.TimeSlotId!.Value) does not, see the GroupBy/.Value gotcha this
		// mirrors elsewhere in this class.
		var nullableIds = timeSlotIds.Select(id => (TimeSlotId?)id).ToList();

		return await Set<Engagement>()
			.Where(e => nullableIds.Contains(e.TimeSlotId)
				&& (e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))
			.ToListAsync(cancellationToken);
	}

	public async Task<List<VolunteerOpportunity>> GetBlockingOpportunitiesForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default)
	{
		var opportunityIds = await Set<VolunteerOpportunity>()
			.Where(vo => vo.OrganizationId == organizationId)
			.Select(vo => vo.Id)
			.ToListAsync(cancellationToken);

		if (opportunityIds.Count == 0)
			return [];

		var now = DateTimeOffset.UtcNow;

		var opportunityIdsWithActiveEngagements = await Set<Engagement>()
			.Where(e => opportunityIds.Contains(e.OpportunityId)
				&& (e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))
			.Select(e => e.OpportunityId)
			.Distinct()
			.ToListAsync(cancellationToken);

		return await Set<VolunteerOpportunity>()
			.Where(vo => vo.OrganizationId == organizationId
				&& (vo.TimeSlots.Any(ts => ts.StartDateTime > now)
					|| opportunityIdsWithActiveEngagements.Contains(vo.Id)))
			.ToListAsync(cancellationToken);
	}

	public async Task<List<VolunteerOpportunity>> GetOpportunitiesForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default) =>
		await Set<VolunteerOpportunity>()
			.Include(vo => vo.TimeSlots)
			.Where(vo => vo.OrganizationId == organizationId)
			.ToListAsync(cancellationToken);

	public async Task<bool> HasOpenReportAsync(
		ReportTargetType targetType,
		Guid targetId,
		UserId reporterId,
		CancellationToken cancellationToken = default) =>
		await Set<Report>()
			.AnyAsync(r => r.TargetType == targetType
				&& r.TargetId == targetId
				&& r.ReporterId == reporterId
				&& r.Status == ReportStatus.Open, cancellationToken);

	public async Task<List<Report>> GetOpenReportsForTargetAsync(
		ReportTargetType targetType,
		Guid targetId,
		CancellationToken cancellationToken = default) =>
		await Set<Report>()
			.Where(r => r.TargetType == targetType
				&& r.TargetId == targetId
				&& r.Status == ReportStatus.Open)
			.ToListAsync(cancellationToken);

	public async Task<List<Report>> GetReportHistoryForTargetAsync(
		ReportTargetType targetType,
		Guid targetId,
		CancellationToken cancellationToken = default) =>
		await Set<Report>()
			.Where(r => r.TargetType == targetType && r.TargetId == targetId)
			.OrderByDescending(r => r.CreatedOn)
			.ToListAsync(cancellationToken);

	public async Task<Organization?> FindOrganizationIncludingDeletedAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default) =>
		await Set<Organization>()
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

	public async Task<VolunteerOpportunity?> FindVolunteerOpportunityIncludingDeletedAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default) =>
		await Set<VolunteerOpportunity>()
			.IgnoreQueryFilters()
			.Include(vo => vo.TimeSlots)
			.FirstOrDefaultAsync(vo => vo.Id == opportunityId, cancellationToken);

	public async Task<User?> FindUserIncludingDeletedAsync(
		UserId userId,
		CancellationToken cancellationToken = default) =>
		await Set<User>()
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

	public async Task<List<User>> GetOrCreateUsersAsync(
		IReadOnlyCollection<UserId> userIds,
		CancellationToken cancellationToken = default)
	{
		var existing = await Set<User>()
			.Where(u => userIds.Contains(u.Id))
			.ToListAsync(cancellationToken);

		var missingIds = userIds.Except(existing.Select(u => u.Id));
		var created = missingIds.Select(User.Create).ToList();

		if (created.Count > 0)
			await Set<User>().AddRangeAsync(created, cancellationToken);

		return [.. existing, .. created];
	}

	public async Task<User> GetOrCreateUserAsync(
		UserId userId,
		string? preferredLanguage,
		CancellationToken cancellationToken = default)
	{
		var existing = await Users.FindAsync(userId, cancellationToken);
		if (existing is not null)
			return existing;

		await Database.ExecuteSqlInterpolatedAsync($@"
			INSERT INTO ""user"" (id, languages, skills, preferred_language)
			VALUES ({userId.Value}, '[]', '[]', {preferredLanguage})
			ON CONFLICT (id) DO NOTHING", cancellationToken);

		return await Users.FindAsync(userId, cancellationToken)
			?? throw new InvalidOperationException($"User '{userId.Value}' was not found immediately after being inserted.");
	}

	public async Task<Engagement?> GetTerminalEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		TimeSlotId? timeSlotId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.FirstOrDefaultAsync(e => e.VolunteerId == volunteerId
				&& e.OpportunityId == opportunityId
				&& e.TimeSlotId == timeSlotId
				&& (e.Status == EngagementStatus.Withdrawn || e.Status == EngagementStatus.Cancelled),
				cancellationToken);

	public async Task<bool> HasPendingInvitationAsync(
		OrganizationId organizationId,
		UserId inviteeId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationInvitation>()
			.AnyAsync(i => i.OrganizationId == organizationId && i.InviteeId == inviteeId && i.Status == InvitationStatus.Pending, cancellationToken);

	public async Task<List<OrganizationInvitation>> GetInvitationsForOrganizationAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationInvitation>()
			.Where(i => i.OrganizationId == organizationId && i.Status != InvitationStatus.Accepted)
			.OrderByDescending(i => i.CreatedOn)
			.ToListAsync(cancellationToken);

	public async Task<List<OrganizationInvitation>> GetPendingInvitationsForUserAsync(
		UserId inviteeId,
		CancellationToken cancellationToken = default) =>
		await Set<OrganizationInvitation>()
			.Where(i => i.InviteeId == inviteeId && i.Status == InvitationStatus.Pending)
			.OrderByDescending(i => i.CreatedOn)
			.ToListAsync(cancellationToken);

	public Task<bool> CanConnectAsync(
		CancellationToken cancellationToken = default) =>
		Database.CanConnectAsync(cancellationToken);

	protected override void OnModelCreating(
		ModelBuilder modelBuilder) =>
			modelBuilder.ApplyConfigurationsFromAssembly(
				Assembly.GetExecutingAssembly());

	public bool HasActiveTransaction =>
		Database.CurrentTransaction != null;

	// CreateExecutionStrategy() + strategy.ExecuteAsync(...) is required here,
	// not just Database.BeginTransactionAsync(...) directly - with
	// EnableRetryOnFailure configured (ServiceCollectionExtensions.cs), EF
	// Core throws on a manually-began transaction unless the begin/operation/
	// commit-or-rollback all run as a single retryable unit. A transient
	// failure re-runs this whole delegate from scratch against a fresh
	// transaction, so `operation` must be safe to invoke again in that case.
	public async Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken = default)
	{
		var strategy = Database.CreateExecutionStrategy();

		return await strategy.ExecuteAsync<TResult>(async ct =>
		{
			await using var transaction = await Database.BeginTransactionAsync(ct);

			try
			{
				TResult result = await operation(ct);

				await transaction.CommitAsync(ct);

				return result;
			}
			catch (Exception)
			{
				await transaction.RollbackAsync(ct);

				throw;
			}
		}, cancellationToken);
	}
}
