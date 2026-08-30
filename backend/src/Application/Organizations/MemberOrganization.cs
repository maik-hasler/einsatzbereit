using Domain.Organizations;

namespace Application.Organizations;

public sealed record MemberOrganization(
	Organization Organization,
	OrganizationMemberRole Role);
