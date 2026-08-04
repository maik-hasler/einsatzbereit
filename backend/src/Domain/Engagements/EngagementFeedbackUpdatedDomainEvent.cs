using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed record EngagementFeedbackUpdatedDomainEvent(
	EngagementId EngagementId,
	UserId VolunteerId,
	VolunteerOpportunityId OpportunityId,
	int Rating)
	: DomainEvent;
