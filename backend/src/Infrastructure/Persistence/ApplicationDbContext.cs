using System.Reflection;
using Application.Common.Persistence;
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

	internal IQueryable<Notification> NotificationsQuery => Set<Notification>().AsNoTracking();

	public async ValueTask<List<Notification>> GetUnreadNotificationsForRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default) =>
		await Set<Notification>()
			.Where(n => n.RecipientId == recipientId && !n.IsRead)
			.ToListAsync(cancellationToken);

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
