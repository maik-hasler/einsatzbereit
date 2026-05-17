namespace Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

public sealed record VolunteerOpportunityDetails(
	Guid Id,
	string Title,
	string Description,
	Guid OrganizationId,
	string OrganizationName,
	string? Street,
	string? HouseNumber,
	string? ZipCode,
	string? City,
	double? Latitude,
	double? Longitude,
	bool IsRemote,
	string Occurrence,
	string ParticipationType,
	string CheckInMethod,
	string? CheckInPin,
	IReadOnlyList<TimeSlotDetail> TimeSlots,
	DateTimeOffset CreatedOn);

public sealed record TimeSlotDetail(
	Guid Id,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants);
