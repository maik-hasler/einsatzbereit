using Domain.Primitives;
using Domain.Users;

namespace Domain.SearchAlerts;

public sealed record SearchAlertMatchesFoundDomainEvent(
	SearchAlertId SearchAlertId,
	UserId RecipientId,
	IReadOnlyList<Guid> OpportunityIds)
	: DomainEvent
{
	// The compiler-generated record Equals would otherwise compare OpportunityIds by
	// reference (List<T>/arrays don't override Equals) - harmless in production, but
	// it fails OutboxMessageSerializationTests' round-trip check, since deserializing
	// always produces a new list instance even with identical contents.
	public bool Equals(SearchAlertMatchesFoundDomainEvent? other) =>
		other is not null &&
		SearchAlertId == other.SearchAlertId &&
		RecipientId == other.RecipientId &&
		OpportunityIds.SequenceEqual(other.OpportunityIds);

	public override int GetHashCode() =>
		HashCode.Combine(SearchAlertId, RecipientId, OpportunityIds.Count);
}
