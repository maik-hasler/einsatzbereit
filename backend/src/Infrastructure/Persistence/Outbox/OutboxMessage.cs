using System.Text.Json;
using Domain.Primitives;

namespace Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessage
{
	public Guid Id { get; init; }

	public string Type { get; init; } = string.Empty;

	public string Content { get; init; } = string.Empty;

	public DateTime OccurredOnUtc { get; init; }

	public DateTime? ProcessedOnUtc { get; set; }

	public string? Error { get; set; }

	public static OutboxMessage FromDomainEvent(
		DomainEvent domainEvent,
		DateTime occurredOnUtc) =>
		new()
		{
			Id = Guid.NewGuid(),
			Type = domainEvent.GetType().FullName!,
			Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
			OccurredOnUtc = occurredOnUtc,
		};

	public DomainEvent ToDomainEvent()
	{
		var type = typeof(DomainEvent).Assembly.GetType(Type)
			?? throw new InvalidOperationException($"Unknown domain event type '{Type}'.");

		return (DomainEvent)(JsonSerializer.Deserialize(Content, type)
			?? throw new InvalidOperationException($"Failed to deserialize outbox message '{Id}' of type '{Type}'."));
	}
}
