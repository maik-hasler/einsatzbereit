using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.CreateTimeSlot.v1;

public sealed record CreateTimeSlotCommand(
	Guid OpportunityId,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants,
	UserId RequestingUserId)
	: ICommand<Guid>;
