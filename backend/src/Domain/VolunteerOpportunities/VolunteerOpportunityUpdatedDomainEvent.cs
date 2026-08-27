using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityUpdatedDomainEvent(
	VolunteerOpportunityId OpportunityId,
	TimeSlotId? TimeSlotId)
	: DomainEvent;
