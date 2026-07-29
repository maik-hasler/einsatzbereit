using Domain.Primitives;

namespace Domain.Organizations;

public sealed record OrganizationDeletedDomainEvent(
	OrganizationId OrganizationId)
	: DomainEvent;
