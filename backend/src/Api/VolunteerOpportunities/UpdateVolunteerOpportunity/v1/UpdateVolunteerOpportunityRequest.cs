using System.ComponentModel.DataAnnotations;

namespace Api.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

public sealed record UpdateVolunteerOpportunityRequest(
	[MaxLength(200)] string? Title,
	[MaxLength(5000)] string? Description,
	bool IsRemote,
	[MaxLength(200)] string? Street,
	[MaxLength(20)] string? HouseNumber,
	[MaxLength(10)] string? ZipCode,
	[MaxLength(100)] string? City,
	string Occurrence,
	string ParticipationType,
	string CheckInMethod,
	string? Category,
	IReadOnlyList<string>? Tags);
