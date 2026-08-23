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

	public DateTime? ClaimedOnUtc { get; set; }

	public string? Error { get; set; }

	public int AttemptCount { get; set; }

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		Converters = { new ValueObjectIdJsonConverterFactory() },
	};

	public static OutboxMessage FromDomainEvent(
		DomainEvent domainEvent,
		DateTime occurredOnUtc) =>
		new()
		{
			Id = Guid.CreateVersion7(),
			Type = domainEvent.GetType().FullName!,
			Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
			OccurredOnUtc = occurredOnUtc,
		};

	public DomainEvent ToDomainEvent()
	{
		var type = typeof(DomainEvent).Assembly.GetType(Type)
			?? throw new InvalidOperationException($"Unknown domain event type '{Type}'.");

		return (DomainEvent)(JsonSerializer.Deserialize(Content, type, SerializerOptions)
			?? throw new InvalidOperationException($"Failed to deserialize outbox message '{Id}' of type '{Type}'."));
	}
}
