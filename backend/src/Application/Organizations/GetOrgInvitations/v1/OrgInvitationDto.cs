namespace Application.Organizations.GetOrgInvitations.v1;

public sealed record OrgInvitationDto(
	Guid Id,
	Guid InviteeId,
	string InviteeName,
	string IntendedRole,
	string Status,
	DateTimeOffset CreatedOn,
	DateTimeOffset ExpiresOn);
