using Domain.Organizations;
using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed record VolunteerOpportunityCancelledDomainEvent(
	VolunteerOpportunityId OpportunityId,
	OrganizationId OrganizationId,
	string? Reason)
	: DomainEvent;
