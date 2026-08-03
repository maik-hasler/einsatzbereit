using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityGeocodingRequestedDomainEvent(
	VolunteerOpportunityId OpportunityId)
	: DomainEvent;
