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

	string RequestingUserRole,

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
