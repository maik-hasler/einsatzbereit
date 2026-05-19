using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.DeleteTimeSlot.v1;

public sealed record DeleteTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId)
	: ICommand<bool>;
