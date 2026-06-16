namespace Application.Organizations.GetPublicOrganizationProfile.v1;

public sealed record PublicOrganizationProfileResponse(
	Guid Id,
	string Name,
	string? Description,
	string? ContactEmail,
	string? ContactPhone,
	string? Website,
	PublicAddressDto? Address,
	IReadOnlyList<PublicOpportunitySummaryDto> OpenOpportunities);

public sealed record PublicAddressDto(
	string Street,
	string HouseNumber,
	string ZipCode,
	string City);

public sealed record PublicOpportunitySummaryDto(
	Guid Id,
	string Title,
	string? Description,
	string? Street,
	string? HouseNumber,
	string? ZipCode,
	string? City,
	bool IsRemote,
	string Occurrence,
	string ParticipationType,
	DateTimeOffset CreatedOn);
