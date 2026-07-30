using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteTimeSlot.v1;

public sealed record DeleteTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId,
	UserId RequestingUserId,
	SeriesEditScope Scope = SeriesEditScope.Only)
	: ICommand<DeleteTimeSlotResult>;

public sealed record DeleteTimeSlotResult(IReadOnlyList<Guid> DeletedTimeSlotIds);
