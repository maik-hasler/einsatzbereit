using Application.Common;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.Interceptors;

internal sealed class DomainEventInterceptor(
	IDomainEventDispatcher dispatcher)
	: SaveChangesInterceptor
{
	public override async ValueTask<int> SavedChangesAsync(
		SaveChangesCompletedEventData eventData,
		int result,
		CancellationToken cancellationToken = default)
	{
		if (eventData.Context is not null)
		{
			var events = eventData.Context.ChangeTracker
				.Entries<IAggregateRoot>()
				.SelectMany(e => e.Entity.Events)
				.ToList();

			foreach (var entry in eventData.Context.ChangeTracker.Entries<IAggregateRoot>())
			{
				entry.Entity.ClearEvents();
			}

			await dispatcher.DispatchAsync(events, cancellationToken);
		}

		return await base.SavedChangesAsync(eventData, result, cancellationToken);
	}
}
