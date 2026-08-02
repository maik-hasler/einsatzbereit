namespace Application.Common.Persistence;

public interface IUnitOfWork
	: IDisposable
{
	bool HasActiveTransaction { get; }

	// The database connection retries transient failures (EnableRetryOnFailure),
	// which requires every user-initiated transaction to run as a single
	// retryable unit - EF Core throws if a transaction is began manually while
	// a retrying execution strategy is configured. This wraps begin/commit
	// (rollback on failure) and the caller's operation together so a retry
	// re-runs the whole thing from scratch instead of resuming a half-open
	// transaction.
	Task<TResult> ExecuteInTransactionAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken = default);

	Task<int> SaveChangesAsync(
		CancellationToken cancellationToken = default);
}
