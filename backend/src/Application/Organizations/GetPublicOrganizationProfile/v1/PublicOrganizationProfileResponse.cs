namespace Application.Organizations.GetPublicOrganizationProfile.v1;

public sealed record PublicOrganizationProfileResponse(
	Guid Id,
	string Name,
	string? Description,
	string? ContactEmail,
	string? ContactPhone,
	string? Website,
	PublicAddressDto? Address,
	IReadOnlyList<PublicOpportunitySummaryDto> OpenOpportunities,
	string? LogoUrl);

public sealed record PublicAddressDto(
	string Street,
	string HouseNumber,
	string ZipCode,
	string City);

public sealed record PublicOpportunitySummaryDto(
	Guid Id,
	string TitleDe,
	string? TitleEn,
	string? DescriptionDe,
	string? DescriptionEn,
	string? Street,
	string? HouseNumber,
	string? ZipCode,
	string? City,
	bool IsRemote,
	string Occurrence,
	string ParticipationType,
	DateTimeOffset CreatedOn,
	string? Category,
	int? TotalMaxParticipants,
	int CurrentParticipantCount);
