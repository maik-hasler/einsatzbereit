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
