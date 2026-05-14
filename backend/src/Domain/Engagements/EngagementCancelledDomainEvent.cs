using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed record EngagementCancelledDomainEvent(
	EngagementId EngagementId,
	UserId VolunteerId,
	VolunteerOpportunityId OpportunityId,
	string? Reason)
	: DomainEvent;
