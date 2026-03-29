using System.Reflection;
using Application.Abstractions;
using Domain.Bedarfe;
using Domain.Organisationen;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : DbContext(options),
    IUnitOfWork,
    IApplicationDbContext
{
    public IAggregateRepository<Bedarf, BedarfId> Bedarfe
        => new AggregateRepository<Bedarf, BedarfId>(
            Set<Bedarf>(),
            Set<Bedarf>(),
            bedarf => bedarf.Id);
    
    public IQueryable<Bedarf> BedarfeQuery => Set<Bedarf>().AsNoTracking();

    public IAggregateRepository<Organisation, OrganisationId> Organisationen
        => new AggregateRepository<Organisation, OrganisationId>(
            Set<Organisation>(),
            Set<Organisation>(),
            organisation => organisation.Id);
    
    public IQueryable<Organisation> OrganisationenQuery => Set<Organisation>().AsNoTracking();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfigurationsFromAssembly(
                Assembly.GetExecutingAssembly());
}