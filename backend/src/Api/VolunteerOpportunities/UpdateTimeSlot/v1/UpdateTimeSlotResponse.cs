namespace Api.VolunteerOpportunities.UpdateTimeSlot.v1;

public sealed record UpdateTimeSlotResponse(
	int UpdatedCount,
	IReadOnlyList<Guid> SkippedTimeSlotIds);
