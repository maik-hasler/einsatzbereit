namespace Api.VolunteerOpportunities.UpdateTimeSlot.v1;

public sealed record UpdateTimeSlotRequest(
	DateTimeOffset? StartDateTime,
	DateTimeOffset? EndDateTime,
	int? MaxParticipants,
	string? Scope);
