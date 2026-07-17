using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed record EngagementConfirmedDomainEvent(
	EngagementId EngagementId,
	UserId VolunteerId,
	VolunteerOpportunityId OpportunityId)
	: DomainEvent;
