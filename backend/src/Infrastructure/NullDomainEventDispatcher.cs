using Application.Common;
using Domain.Primitives;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

internal sealed class NullDomainEventDispatcher(
	ILogger<NullDomainEventDispatcher> logger)
	: IDomainEventDispatcher
{
	public ValueTask DispatchAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default)
	{
		foreach (var @event in events)
		{
			logger.LogDebug("Domain event raised: {EventType}", @event.GetType().Name);
		}

		return ValueTask.CompletedTask;
	}
}
