using Domain.Primitives;

namespace Application.Common;

public interface IDomainEventDispatcher
{
	ValueTask DispatchAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default);
}
