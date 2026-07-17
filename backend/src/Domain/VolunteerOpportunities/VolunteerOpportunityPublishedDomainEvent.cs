using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityPublishedDomainEvent(
	VolunteerOpportunityId OpportunityId,
	OrganizationId OrganizationId)
	: DomainEvent;
