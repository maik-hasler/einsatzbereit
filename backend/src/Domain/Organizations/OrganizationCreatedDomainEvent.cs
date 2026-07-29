using Domain.Primitives;

namespace Domain.Organizations;

public sealed record OrganizationCreatedDomainEvent(
	OrganizationId OrganizationId)
	: DomainEvent;
