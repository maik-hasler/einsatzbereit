namespace Application.Organizations;

public sealed record OrganizationMembershipSummary(
	Guid OrganizationId,
	string OrganizationName,
	string Role);
