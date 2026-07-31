using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence.Outbox;
// ApiClient.cs (generated, same "IntegrationTests" namespace) also declares
// "DomainEvent" and "OrganizationId" DTO types, which would otherwise shadow
// their Domain counterparts.
using CoreDomainEvent = Domain.Primitives.DomainEvent;
using CoreOrganizationId = Domain.Organizations.OrganizationId;

namespace IntegrationTests;

// Pure (de)serialization round-trip - no database needed - but OutboxMessage is
// internal to Infrastructure (InternalsVisibleTo only grants IntegrationTests, see
// Infrastructure.csproj), so this has to live here rather than in a project that
// can't see it.
//
// Regression coverage for #1336: every ID a DomainEvent carries is a Guid-backed
// value-object struct with a private constructor, which System.Text.Json's
// default reflection serializer cannot bind on deserialize - it silently produces
// Guid.Empty instead of throwing (fixed by ValueObjectIdJsonConverterFactory, see
// its own comment for the #1038 story). Parameterised across every DomainEvent
// subclass so a newly added one is covered by construction, not by remembering to
// add it here.
public class OutboxMessageSerializationTests
{
	[Test]
	[MethodDataSource(nameof(AllDomainEvents))]
	public void FromDomainEvent_ThenToDomainEvent_ShouldRoundTripEveryValueObjectId(
		CoreDomainEvent domainEvent)
	{
		var message = OutboxMessage.FromDomainEvent(domainEvent, DateTime.UtcNow);

		var roundTripped = message.ToDomainEvent();

		roundTripped.Should().Be(domainEvent);
	}

	[Test]
	public void AllDomainEvents_ShouldCoverEveryDomainEventSubclass()
	{
		// Guards against the exact failure mode this issue describes: a new
		// DomainEvent subclass silently never getting a round-trip test.
		var allSubclassNames = typeof(CoreDomainEvent).Assembly
			.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false } && typeof(CoreDomainEvent).IsAssignableFrom(t))
			.Select(t => t.FullName)
			.ToList();

		var coveredNames = AllDomainEvents().Select(e => e.GetType().FullName).ToList();

		coveredNames.Should().BeEquivalentTo(allSubclassNames,
			"every DomainEvent subclass must have a round-trip case in AllDomainEvents()");
	}

	public static IEnumerable<CoreDomainEvent> AllDomainEvents()
	{
		yield return new EngagementCancelledDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), "Reason");
		yield return new EngagementCheckInUndoneDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		yield return new EngagementCheckedInDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		yield return new EngagementConfirmedDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		yield return new EngagementFeedbackSubmittedDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), 5);
		yield return new EngagementReactivatedDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		yield return new EngagementReminderDueDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), TimeSlotId.New());
		yield return new EngagementWithdrawnDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		yield return new OrganizationInvitationAcceptedDomainEvent(
			OrganizationInvitationId.New(), CoreOrganizationId.New(), UserId.New());
		yield return new OrganizationInvitationDeclinedDomainEvent(
			OrganizationInvitationId.New(), CoreOrganizationId.New(), UserId.New());
		yield return new OrganizationVerificationRevokedDomainEvent(CoreOrganizationId.New());
		yield return new OrganizationVerifiedDomainEvent(CoreOrganizationId.New());
		yield return new VolunteerOpportunityCancelledDomainEvent(
			VolunteerOpportunityId.New(), CoreOrganizationId.New(), "Reason");
		yield return new VolunteerOpportunityPublishedDomainEvent(
			VolunteerOpportunityId.New(), CoreOrganizationId.New());
		yield return new VolunteerOpportunityUnpublishedDomainEvent(
			VolunteerOpportunityId.New(), CoreOrganizationId.New());
	}
}
