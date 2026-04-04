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
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();
            
            await unitOfWork.SaveChangesAsync(cancellationToken);
            
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return response;
        }
        catch (Exception)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);

            throw;
        }
    }
}