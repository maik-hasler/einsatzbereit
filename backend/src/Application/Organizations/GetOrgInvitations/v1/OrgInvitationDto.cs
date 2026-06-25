namespace Application.Organizations.GetOrgInvitations.v1;

public sealed record OrgInvitationDto(
	Guid Id,
	Guid InviteeId,
	string InviteeName,
	string Status,
	DateTimeOffset CreatedOn);
