using Domain.Primitives;

namespace Domain.Organizations;

public sealed record OrganizationVerificationRevokedDomainEvent(
	OrganizationId OrganizationId)
	: DomainEvent;
