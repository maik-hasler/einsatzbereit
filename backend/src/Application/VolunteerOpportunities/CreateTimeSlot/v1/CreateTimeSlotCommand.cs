using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateTimeSlot.v1;

public sealed record CreateTimeSlotCommand(
	Guid OpportunityId,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int MaxParticipants,
	UserId RequestingUserId,
	string? RecurrenceFrequency = null,
	int RecurrenceCount = 1)
	: ICommand<IReadOnlyList<TimeSlot>>;
