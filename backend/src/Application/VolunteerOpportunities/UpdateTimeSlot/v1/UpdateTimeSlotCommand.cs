using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateTimeSlot.v1;

public sealed record UpdateTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId,
	DateTimeOffset? StartDateTime,
	DateTimeOffset? EndDateTime,
	int? MaxParticipants,
	UserId RequestingUserId,
	SeriesEditScope Scope = SeriesEditScope.Only)
	: ICommand<UpdateTimeSlotResult>;

public sealed record UpdateTimeSlotResult(
	int UpdatedCount,
	IReadOnlyList<Guid> SkippedTimeSlotIds);
