namespace Application.Invitations.GetMyInvitations.v1;

public sealed record MyInvitationDto(
	Guid Id,
	Guid OrganizationId,
	string OrganizationName,
	DateTimeOffset CreatedOn);
