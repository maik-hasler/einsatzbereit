namespace Application.Organizations.GetOrganizationDetails.v1;

public sealed record OrganizationDetailsResponse(
	Guid Id,
	string Name,
	string? Description,
	string? ContactEmail,
	string? ContactPhone,
	string? Website,
	string? LogoUrl,
	AddressDto? Address,
	DateTimeOffset CreatedOn,
	IReadOnlyList<OrganizationMemberDto> Members,
	// Answered from organization_membership, independent of the Keycloak-sourced
	// Members roster below - so the org app shell can gate Organizer-only nav and
	// actions without needing Keycloak's member lookup to succeed (#1709).
	string RequestingUserRole,
	// True when Keycloak's member lookup failed and Members was filled in from
	// organization_membership instead (id + role only, no username/email/name -
	// those live in Keycloak, not locally) rather than left empty (#1709).
	bool MembersUnavailable);

public sealed record AddressDto(
	string Street,
	string HouseNumber,
	string ZipCode,
	string City);

public sealed record OrganizationMemberDto(
	Guid UserId,
	string Username,
	string? FirstName,
	string? LastName,
	string Email,
	bool IsOrganisator,
	string Role);
