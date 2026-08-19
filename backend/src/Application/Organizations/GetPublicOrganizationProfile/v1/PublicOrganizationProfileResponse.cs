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
	// Added for #2054: without these, the org-profile and "more from this
	// organization" cards had no date/deadline to show at all, unlike every
	// card backed by VolunteerOpportunitySummary - the repository call below
	// already resolves both, they were just never carried onto this DTO.
	DateTimeOffset? ValidUntil,
	DateTimeOffset? NextTimeSlotStart,
	string? Category,
	int? TotalMaxParticipants,
	int CurrentParticipantCount);
