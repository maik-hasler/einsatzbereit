namespace Api.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

public sealed record CreateVolunteerOpportunityResponse(
	Guid Id,
	string Title,
	string Description,
	Guid OrganizationId,
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
	DateTimeOffset CreatedOn);
