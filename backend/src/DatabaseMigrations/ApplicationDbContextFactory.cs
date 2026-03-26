using System.Reflection;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DatabaseMigrations;

internal sealed class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(
        string[] args) => new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(b => b.MigrationsAssembly(
                    Assembly.GetExecutingAssembly().FullName))
                .UseSnakeCaseNamingConvention()
                .Options);
}