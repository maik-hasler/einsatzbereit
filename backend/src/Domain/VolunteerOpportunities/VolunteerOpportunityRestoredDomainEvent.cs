using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityRestoredDomainEvent(
	VolunteerOpportunityId OpportunityId,
	OrganizationId OrganizationId)
	: DomainEvent;
