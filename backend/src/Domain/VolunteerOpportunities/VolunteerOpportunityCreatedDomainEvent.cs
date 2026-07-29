using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityCreatedDomainEvent(
	VolunteerOpportunityId OpportunityId,
	OrganizationId OrganizationId)
	: DomainEvent;
