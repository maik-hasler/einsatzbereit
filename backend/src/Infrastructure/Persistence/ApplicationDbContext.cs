using System.Reflection;
using Application.Abstractions;
using Domain.Bedarfe;
using Domain.Organisationen;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : DbContext(options),
    IUnitOfWork
{
    public DbSet<Bedarf> Bedarfe => Set<Bedarf>();

    public DbSet<Organisation> Organisationen => Set<Organisation>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfigurationsFromAssembly(
                Assembly.GetExecutingAssembly());
}