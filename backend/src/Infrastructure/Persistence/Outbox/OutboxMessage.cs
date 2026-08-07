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

	// Stamped by OutboxProcessorJob.ClaimBatchAsync the instant a batch is claimed,
	// then left alone for the rest of dispatch (#1729) - the FOR UPDATE SKIP LOCKED
	// row lock is only held for that brief claim+stamp transaction, not across the
	// SMTP-heavy dispatch that follows it outside any open transaction. A second
	// replica's claim query treats a row with a recent ClaimedOnUtc as already
	// in flight and skips it; one older than OutboxOptions.ClaimTimeoutSeconds is
	// treated as abandoned (the claiming process crashed) and reclaimed.
	public DateTime? ClaimedOnUtc { get; set; }

	public string? Error { get; set; }

	// Incremented on every failed dispatch attempt; once it reaches OutboxOptions.MaxAttempts
	// the message is moved to a terminal dead-letter state instead of retrying forever (#1317).
	public int AttemptCount { get; set; }

	// See ValueObjectIdJsonConverterFactory: without it, the Guid-backed value-object IDs
	// (VolunteerOpportunityId, UserId, ...) that domain events carry silently deserialize as
	// Guid.Empty instead of round-tripping their real value.
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		Converters = { new ValueObjectIdJsonConverterFactory() },
	};

	public static OutboxMessage FromDomainEvent(
		DomainEvent domainEvent,
		DateTime occurredOnUtc) =>
		new()
		{
			// Guid.CreateVersion7() (time-ordered), not Guid.NewGuid() (v4, random)
			// - matches every other entity's id generation and avoids random
			// B-tree inserts on the fastest-growing table in the schema (#1201).
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
