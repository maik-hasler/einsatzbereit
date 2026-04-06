using System.Reflection;
using Application.Common.Persistence;
using Domain.VolunteerOpportunities;
using Domain.Organizations;
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
            Set<VolunteerOpportunity>(),
            vo => vo.Id);

    internal IQueryable<VolunteerOpportunity> VolunteerOpportunitiesQuery => Set<VolunteerOpportunity>().AsNoTracking();

    public IAggregateRepository<Organization, OrganizationId> Organizations
        => new AggregateRepository<Organization, OrganizationId>(
            Set<Organization>(),
            Set<Organization>(),
            org => org.Id);

    internal IQueryable<Organization> OrganizationsQuery => Set<Organization>().AsNoTracking();

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
