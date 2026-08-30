using Domain.Organizations;

namespace Application.Organizations.CreateInvitation.v1;

public sealed record CreateInvitationResult(
	OrganizationInvitationId Id,
	DateTimeOffset ExpiresOn);
