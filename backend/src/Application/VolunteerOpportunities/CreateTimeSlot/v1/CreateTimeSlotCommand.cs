using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.CreateTimeSlot.v1;

public sealed record CreateTimeSlotCommand(
	Guid OpportunityId,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants)
	: ICommand<Guid>;
