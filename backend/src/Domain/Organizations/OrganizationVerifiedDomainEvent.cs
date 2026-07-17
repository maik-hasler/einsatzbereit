using Domain.Primitives;

namespace Domain.Organizations;

public sealed record OrganizationVerifiedDomainEvent(
	OrganizationId OrganizationId)
	: DomainEvent;
