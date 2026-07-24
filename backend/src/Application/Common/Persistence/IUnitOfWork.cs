namespace Application.Common.Persistence;

public interface IUnitOfWork
	: IDisposable
{
	bool HasActiveTransaction { get; }

	Task BeginTransactionAsync(
		CancellationToken cancellationToken = default);

	Task CommitTransactionAsync(
		CancellationToken cancellationToken = default);

	Task RollbackTransactionAsync(
		CancellationToken cancellationToken = default);

	Task<int> SaveChangesAsync(
		CancellationToken cancellationToken = default);
}
