namespace Application.Common.Messaging;

public interface IPipelineBehavior<TRequest, TResponse>
{
    ValueTask<TResponse> Handle(
        TRequest request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default);
}