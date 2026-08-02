using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Common.PipelineBehaviors;

internal sealed class TransactionPipelineBehavior<TCommand, TResponse>(
	IUnitOfWork unitOfWork)
	: IPipelineBehavior<TCommand, TResponse>
	where TCommand : ICommand<TResponse>
{
	public async ValueTask<TResponse> Handle(
		TCommand request,
		Func<ValueTask<TResponse>> next,
		CancellationToken cancellationToken = default)
	{
		// A nested Send() call (see Sender.AmbientScope) shares the caller's
		// IUnitOfWork/DbContext and therefore its transaction. Let the
		// outermost command own the single begin/save/commit-or-rollback so
		// nested writes commit or roll back atomically with it, instead of
		// each nested command opening (and prematurely committing) its own.
		if (unitOfWork.HasActiveTransaction)
		{
			return await next();
		}

		return await unitOfWork.ExecuteInTransactionAsync(
			async ct =>
			{
				var response = await next();

				await unitOfWork.SaveChangesAsync(ct);

				return response;
			},
			cancellationToken);
	}
}
