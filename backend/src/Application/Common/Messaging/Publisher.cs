using System.Collections.Concurrent;
using Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.Messaging;

internal sealed class Publisher(
    IServiceProvider serviceProvider)
    : IPublisher
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeDictionary = new();

    private static readonly ConcurrentDictionary<Type, Type> WrapperTypeDictionary = new();

    public async Task Publish(
        INotification notification,
        CancellationToken cancellationToken = default)
    {
        var domainEventType = notification.GetType();

        var handlerType = HandlerTypeDictionary.GetOrAdd(
            domainEventType,
            et => typeof(INotificationHandler<>).MakeGenericType(et));

        using var scope = serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
	        if (handler is null)
	        {
		        continue;
	        }

	        var handlerName = handler.GetType().Name;

	        var handlerWrapper = HandlerWrapper.Create(handler, domainEventType);

	        await handlerWrapper.Handle(notification, cancellationToken);
        }
    }

    private abstract class HandlerWrapper
    {
        public abstract Task Handle(INotification iNotification, CancellationToken cancellationToken);

        public static HandlerWrapper Create(object handler, Type domainEventType)
        {
            var wrapperType = WrapperTypeDictionary.GetOrAdd(
                domainEventType,
                et => typeof(HandlerWrapper<>).MakeGenericType(et));

            return (HandlerWrapper)Activator.CreateInstance(wrapperType, handler)!;
        }
    }

    private sealed class HandlerWrapper<T>(object handler) : HandlerWrapper where T : INotification
    {
        private readonly INotificationHandler<T> _handler = (INotificationHandler<T>)handler;

        public override async Task Handle(INotification iNotification, CancellationToken cancellationToken)
        {
            await _handler.Handle((T)iNotification, cancellationToken);
        }
    }
}
