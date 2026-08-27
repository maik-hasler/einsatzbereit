namespace Api.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability.v1;

public sealed record GetVolunteerOpportunityDateAvailabilityRequest(
	DateTimeOffset From,
	DateTimeOffset To,
	// Unused - kept only so the currently-committed generated clients
	// (frontend/src/client/api-client.ts, IntegrationTests/ApiClient.cs) stay
	// wire-compatible until their next NSwag regeneration, which can drop this
	// property. The caller's zone comes from the X-Timezone header instead
	// (see the endpoint) - a single scalar offset can't be right for every
	// slot in a multi-week window once a DST transition falls inside it (#2203).
	int? UtcOffsetMinutes,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	string[]? Categories,
	string? Tag,
	string? Keyword);
