using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.UpdateTimeSlot.v1;

public sealed record UpdateTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants,
	UserId RequestingUserId)
	: ICommand<bool>;
