using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Messaging;

internal sealed class Sender(
    IServiceProvider serviceProvider)
    : ISender
{
    private static readonly ConcurrentDictionary<Type, IHandlerWrapper> HandlerWrapperCache = [];
    
    public async ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();

        var handlerWrapper = HandlerWrapperCache.GetOrAdd(requestType, CreateHandlerWrapper);
        
        using var scope = serviceProvider.CreateScope();

        var result = await handlerWrapper.HandleAsync(request, scope, cancellationToken);

        return (TResponse)result!;
    }

    private static IHandlerWrapper CreateHandlerWrapper(
        Type requestType)
    {
        var requestInterface = requestType.GetInterfaces()
           .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?? throw new InvalidOperationException($"Request type '{requestType.Name}' does not implement IRequest<TResponse>");
        
        var responseType = requestInterface.GetGenericArguments()[0];
        
        var wrapperType = typeof(HandlerWrapper<,>).MakeGenericType(requestType, responseType);
        
        return (IHandlerWrapper)Activator.CreateInstance(wrapperType)!;
    }

    private interface IHandlerWrapper
    {
        public ValueTask<object?> HandleAsync(
            object request,
            IServiceScope scope,
            CancellationToken cancellationToken = default);
    }

    private sealed class HandlerWrapper<TRequest, TResponse>
        : IHandlerWrapper
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<object?> HandleAsync(
            object request,
            IServiceScope scope,
            CancellationToken cancellationToken = default)
        {
            var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            
            var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
                .ToArray();
            
            var pipeline = () => handler.Handle((TRequest)request, cancellationToken);
            
            foreach (var behavior in behaviors)
            {
                var next = pipeline;

                pipeline = () => behavior.Handle((TRequest)request, next, cancellationToken);
            }

            return await pipeline();
        }
    }
}