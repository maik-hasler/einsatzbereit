namespace Api.VolunteerOpportunities.DeleteTimeSlot.v1;

public sealed record DeleteTimeSlotResponse(IReadOnlyList<Guid> DeletedTimeSlotIds);
