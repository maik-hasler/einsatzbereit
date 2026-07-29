using Domain.Primitives;

namespace Domain.Organizations;

public sealed record OrganizationRestoredDomainEvent(
	OrganizationId OrganizationId)
	: DomainEvent;
