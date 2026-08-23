namespace Application.Common.Persistence;

public interface IUnitOfWork
	: IDisposable
{
	bool HasActiveTransaction { get; }

	Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken = default);

	Task<int> SaveChangesAsync(
		CancellationToken cancellationToken = default);
}
