namespace Application.Common.Persistence;

public interface IUnitOfWork
    : IDisposable
{
    Task BeginTransactionAsync(
        CancellationToken cancellationToken = default);
    
    Task CommitTransactionAsync(
        CancellationToken cancellationToken = default);
    
    Task RollbackTransactionAsync(
        CancellationToken cancellationToken = default);
    
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}