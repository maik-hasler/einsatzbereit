using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.DeleteTimeSlot.v1;

public sealed record DeleteTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId,
	UserId RequestingUserId)
	: ICommand<bool>;
