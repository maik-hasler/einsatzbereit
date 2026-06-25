using System.Reflection;
using Application.Common.Persistence;
using Domain.Achievements;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
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

	public async Task<bool> HasAchievementAsync(
		UserId userId,
		string badgeName,
		CancellationToken cancellationToken = default) =>
		await Set<Achievement>()
			.AnyAsync(a => a.UserId == userId && a.Name == badgeName, cancellationToken);

	public async Task<bool> HasEngagementAsync(
		UserId volunteerId,
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default) =>
		await Set<Engagement>()
			.AnyAsync(e => e.VolunteerId == volunteerId && e.OpportunityId == opportunityId, cancellationToken);

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

	public async Task BeginTransactionAsync(
		CancellationToken cancellationToken = default) =>
			await Database.BeginTransactionAsync(cancellationToken);

	public async Task CommitTransactionAsync(
		CancellationToken cancellationToken = default)
	{
		var currentTransaction = Database.CurrentTransaction;

		if (currentTransaction != null)
		{
			await currentTransaction.CommitAsync(cancellationToken);
		}
	}

	public async Task RollbackTransactionAsync(
		CancellationToken cancellationToken = default)
	{
		var currentTransaction = Database.CurrentTransaction;

		if (currentTransaction != null)
		{
			await currentTransaction.RollbackAsync(cancellationToken);
		}
	}
}
