using AwesomeAssertions;
using Domain.SearchAlerts;
using Domain.Users;

namespace Application.UnitTests.SearchAlerts;

public class SearchAlertMatchesFoundDomainEventTests
{
	[Test]
	public void Equals_ShouldReturnTrue_WhenOpportunityIdsHaveEqualContentButDifferentListInstances()
	{
		// Regression guard: the compiler-generated record Equals would otherwise
		// compare OpportunityIds by reference (List<T>/arrays don't override
		// Equals), which broke OutboxMessageSerializationTests' round-trip
		// check - deserializing always produces a new list instance even with
		// identical contents.
		var searchAlertId = SearchAlertId.New();
		var recipientId = UserId.New();
		var opportunityId = Guid.NewGuid();

		var first = new SearchAlertMatchesFoundDomainEvent(searchAlertId, recipientId, new List<Guid> { opportunityId });
		var second = new SearchAlertMatchesFoundDomainEvent(searchAlertId, recipientId, [opportunityId]);

		first.Should().Be(second);
	}

	[Test]
	public void Equals_ShouldReturnFalse_WhenOpportunityIdsDiffer()
	{
		var searchAlertId = SearchAlertId.New();
		var recipientId = UserId.New();

		var first = new SearchAlertMatchesFoundDomainEvent(searchAlertId, recipientId, [Guid.NewGuid()]);
		var second = new SearchAlertMatchesFoundDomainEvent(searchAlertId, recipientId, [Guid.NewGuid()]);

		first.Should().NotBe(second);
	}
}
