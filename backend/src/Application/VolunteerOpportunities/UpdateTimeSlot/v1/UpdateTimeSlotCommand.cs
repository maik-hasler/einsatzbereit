using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.UpdateTimeSlot.v1;

public sealed record UpdateTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants)
	: ICommand<bool>;
