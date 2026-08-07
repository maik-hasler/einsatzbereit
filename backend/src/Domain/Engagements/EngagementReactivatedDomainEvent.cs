using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

// IsSlotSignUp is denormalized here (einsatzbereit#1729) rather than looked up
// from the Engagement when this event is dispatched, so the outbox-delivered
// volunteer confirmation email (EngagementVolunteerConfirmationHelper) can pick
// the right template without an extra query.
public sealed record EngagementReactivatedDomainEvent(
	EngagementId EngagementId,
	UserId VolunteerId,
	VolunteerOpportunityId OpportunityId,
	bool IsSlotSignUp)
	: DomainEvent;
