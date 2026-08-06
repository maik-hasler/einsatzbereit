using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateTimeSlot.v1;

internal sealed class CreateTimeSlotCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CreateTimeSlotCommand, IReadOnlyList<TimeSlot>>
{
	private const int MaxRecurrenceCount = 52;

	public async ValueTask<IReadOnlyList<TimeSlot>> Handle(
		CreateTimeSlotCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var count = request.RecurrenceFrequency is null
			? 1
			: Math.Clamp(request.RecurrenceCount, 1, MaxRecurrenceCount);
		var duration = request.EndDateTime - request.StartDateTime;
		var slots = new List<TimeSlot>(count);
		var now = DateTimeOffset.UtcNow;
		var timeZone = ResolveTimeZone(request.Timezone);

		// A SeriesId links the generated occurrences so they can later be
		// edited/cancelled together (einsatzbereit#1058) - only meaningful once
		// there's more than one occurrence to link.
		Guid? seriesId = count > 1 ? Guid.CreateVersion7() : null;

		for (var i = 0; i < count; i++)
		{
			var start = Advance(request.StartDateTime, request.RecurrenceFrequency, i, timeZone);
			var end = start + duration;
			slots.Add(opportunity.AddTimeSlot(
				start, end, request.MaxParticipants, now,
				seriesId, request.RecurrenceFrequency, count).GetValueOrThrow());
		}

		return slots;
	}

	// Advancing in UTC via AddDays/AddMonths carries the origin's UTC offset forward
	// unchanged - it has no notion of a time zone, so a recurrence crossing a DST
	// transition silently drifts by an hour in the organizer's local calendar
	// (#1160). Advancing the organizer's own local wall-clock time instead, then
	// re-resolving the UTC offset that applies at the *advanced* date, keeps every
	// occurrence at the same local time of day regardless of DST.
	private static DateTimeOffset Advance(DateTimeOffset origin, string? frequency, int steps, TimeZoneInfo timeZone)
	{
		if (frequency is null || steps == 0)
			return origin;

		var localOrigin = TimeZoneInfo.ConvertTime(origin, timeZone).DateTime;
		var advancedLocal = frequency.ToUpperInvariant() switch
		{
			"WEEKLY" => localOrigin.AddDays(7 * steps),
			"MONTHLY" => localOrigin.AddMonths(steps),
			_ => localOrigin
		};

		// Npgsql only accepts a DateTimeOffset with Offset == TimeSpan.Zero for a
		// "timestamp with time zone" column (this project doesn't opt into
		// Npgsql's legacy timestamp behavior) - re-resolving the *local* UTC
		// offset above is what makes the recurrence DST-safe, but the
		// DateTimeOffset that carries that non-zero offset can never reach
		// SaveChanges. ToUniversalTime() only changes the representation, not
		// the instant, so the DST-correct point in time is preserved.
		return new DateTimeOffset(advancedLocal, timeZone.GetUtcOffset(advancedLocal)).ToUniversalTime();
	}

	private static TimeZoneInfo ResolveTimeZone(string? ianaId)
	{
		if (string.IsNullOrWhiteSpace(ianaId))
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		}
	}
}
