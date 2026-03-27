using System.Reflection;
using Application.Abstractions;
using Domain.Bedarfe;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : DbContext(options),
    IUnitOfWork
{
    public DbSet<Bedarf> Bedarfe => Set<Bedarf>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfigurationsFromAssembly(
                Assembly.GetExecutingAssembly());
}