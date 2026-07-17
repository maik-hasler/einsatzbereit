using Application.Common;
using Application.Common.Messaging;
using Domain.Primitives;

namespace Infrastructure;

internal sealed class DomainEventDispatcher(
	IPublisher publisher)
	: IDomainEventDispatcher
{
	public async ValueTask DispatchAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default)
	{
		foreach (var @event in events)
		{
			await publisher.Publish(@event, cancellationToken);
		}
	}
}
