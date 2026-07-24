using Domain.Primitives;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.Interceptors;

internal sealed class ConvertDomainEventsToOutboxMessagesInterceptor(
	TimeProvider timeProvider)
	: SaveChangesInterceptor
{
	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

		var aggregatesWithEvents = eventData.Context.ChangeTracker
			.Entries<IAggregateRoot>()
			.Select(e => e.Entity)
			.Where(a => a.Events.Count > 0)
			.ToList();

		if (aggregatesWithEvents.Count > 0)
		{
			var occurredOnUtc = timeProvider.GetUtcNow().UtcDateTime;

			var outboxMessages = aggregatesWithEvents
				.SelectMany(a => a.Events)
				.Select(domainEvent => OutboxMessage.FromDomainEvent(domainEvent, occurredOnUtc));

			eventData.Context.Set<OutboxMessage>().AddRange(outboxMessages);

			foreach (var aggregate in aggregatesWithEvents)
			{
				aggregate.ClearEvents();
			}
		}

		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}
}
