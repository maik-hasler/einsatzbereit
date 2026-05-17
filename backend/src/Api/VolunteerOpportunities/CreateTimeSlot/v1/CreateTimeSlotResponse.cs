namespace Api.VolunteerOpportunities.CreateTimeSlot.v1;

public sealed record CreateTimeSlotResponse(
	Guid Id,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants);
