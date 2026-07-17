using Domain.Primitives;
using Domain.Users;

namespace Domain.Organizations;

public sealed record OrganizationInvitationAcceptedDomainEvent(
	OrganizationInvitationId InvitationId,
	OrganizationId OrganizationId,
	UserId InviteeId)
	: DomainEvent;
