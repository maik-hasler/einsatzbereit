namespace Infrastructure.Persistence;

public interface IApplicationDbContextInitializer
{
    ValueTask MigrateAsync(CancellationToken cancellationToken = default);
}