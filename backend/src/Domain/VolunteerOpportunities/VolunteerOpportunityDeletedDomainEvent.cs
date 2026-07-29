using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityDeletedDomainEvent(
	VolunteerOpportunityId OpportunityId,
	OrganizationId OrganizationId)
	: DomainEvent;
