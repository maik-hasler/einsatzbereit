namespace Application.Messaging;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken = default);
}