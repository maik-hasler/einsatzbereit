using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityUnpublishedDomainEvent(
	VolunteerOpportunityId OpportunityId,
	OrganizationId OrganizationId)
	: DomainEvent;
