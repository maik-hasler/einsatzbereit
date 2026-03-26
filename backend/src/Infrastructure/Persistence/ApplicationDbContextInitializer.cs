using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

internal sealed class ApplicationDbContextInitializer(
    ApplicationDbContext dbContext,
    ILogger<ApplicationDbContextInitializer> logger)
    : IApplicationDbContextInitializer
{
    public async ValueTask MigrateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "An exception occurred while migrating the database");
        }
    }
}