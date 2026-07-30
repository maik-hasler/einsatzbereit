using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateTimeSlot.v1;

public sealed record UpdateTimeSlotCommand(
	Guid OpportunityId,
	Guid TimeSlotId,
	DateTimeOffset? StartDateTime,
	DateTimeOffset? EndDateTime,
	int MaxParticipants,
	UserId RequestingUserId,
	SeriesEditScope Scope = SeriesEditScope.Only)
	: ICommand<UpdateTimeSlotResult>;

/// <summary>
/// UpdatedCount/SkippedTimeSlotIds only differ from the trivial 1/[] case when
/// Scope is ThisAndFollowing/EntireSeries: an occurrence whose active sign-ups
/// already exceed the requested capacity is left untouched rather than failing
/// the whole batch (einsatzbereit#1058).
/// </summary>
public sealed record UpdateTimeSlotResult(
	int UpdatedCount,
	IReadOnlyList<Guid> SkippedTimeSlotIds);
