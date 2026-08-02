using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

// Distinct from EngagementCancelledDomainEvent (raised by every Cancel() call,
// including opportunity/time-slot cascade cancellations that already notify
// the volunteer inline as part of their own async handler): this one is
// raised only when an organizer directly cancels a single engagement
// (Cancel(notifyVolunteer: true)), so the outbox-dispatched notification
// handler for it never double-sends for a cascade cancellation.
public sealed record EngagementCancelledByOrganizerDomainEvent(
	EngagementId EngagementId,
	UserId VolunteerId,
	VolunteerOpportunityId OpportunityId,
	string? Reason)
	: DomainEvent;
