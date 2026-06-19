namespace Api.VolunteerOpportunities.CreateTimeSlot.v1;

public sealed record CreateTimeSlotRequest(
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants,
	string? RecurrenceFrequency,
	int RecurrenceCount);
