using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed class TimeSlot
	: Entity<TimeSlotId>,
		IAuditableEntity
{
	// How far around a slot's own window a check-in against it is honoured
	// (Engagement.CheckIn) - and, for a PINCode opportunity, how long its
	// current PIN keeps covering this slot before VolunteerOpportunity treats
	// the next slot as due and rotates (einsatzbereit#2202). Before the start,
	// so an organizer can check volunteers in as they arrive early; well past
	// the end, since Manual/QRCode check-in commonly happens during a
	// post-event wrap-up rather than the instant the slot ends.
	public static readonly TimeSpan CheckInWindowBefore = TimeSpan.FromHours(1);

	public static readonly TimeSpan CheckInWindowAfter = TimeSpan.FromHours(2);

	public DateTimeOffset StartDateTime { get; private set; }

	public DateTimeOffset EndDateTime { get; private set; }

	public int? MaxParticipants { get; private set; }

	public Guid? SeriesId { get; private set; }

	public string? RecurrenceFrequency { get; private set; }

	public int? RecurrenceCount { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private TimeSlot() : base(default) { }
#pragma warning restore CS8618

	private TimeSlot(
		TimeSlotId id,
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int? maxParticipants,
		Guid? seriesId,
		string? recurrenceFrequency,
		int? recurrenceCount)
		: base(id)
	{
		StartDateTime = startDateTime;
		EndDateTime = endDateTime;
		MaxParticipants = maxParticipants;
		SeriesId = seriesId;
		RecurrenceFrequency = recurrenceFrequency;
		RecurrenceCount = recurrenceCount;
	}

	public static Result<TimeSlot> Create(
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int? maxParticipants,
		DateTimeOffset now,
		Guid? seriesId = null,
		string? recurrenceFrequency = null,
		int? recurrenceCount = null)
	{
		var validation = Validate(startDateTime, endDateTime, maxParticipants, now);
		if (validation.IsFailure)
			return Result.Failure<TimeSlot>(validation.Error);

		return new TimeSlot(
			TimeSlotId.New(),
			startDateTime,
			endDateTime,
			maxParticipants,
			seriesId,
			recurrenceFrequency,
			recurrenceCount);
	}

	public Result Update(DateTimeOffset startDateTime, DateTimeOffset endDateTime, int? maxParticipants, DateTimeOffset now)
	{
		var validation = Validate(startDateTime, endDateTime, maxParticipants, now);
		if (validation.IsFailure)
			return validation;

		StartDateTime = startDateTime;
		EndDateTime = endDateTime;
		MaxParticipants = maxParticipants;
		return Result.Success();
	}

	public Result UpdateCapacity(int? maxParticipants)
	{
		if (maxParticipants is <= 0)
			return Result.Failure(Error.Validation("TimeSlot.MaxParticipantsMustBePositive", "Max participants must be greater than zero."));

		MaxParticipants = maxParticipants;
		return Result.Success();
	}

	private static Result Validate(DateTimeOffset startDateTime, DateTimeOffset endDateTime, int? maxParticipants, DateTimeOffset now)
	{
		if (startDateTime <= now)
			return Result.Failure(Error.Validation("TimeSlot.StartMustBeFuture", "Start date must be in the future."));

		if (endDateTime <= startDateTime)
			return Result.Failure(Error.Validation("TimeSlot.EndMustBeAfterStart", "End date must be after start date."));

		if (maxParticipants is <= 0)
			return Result.Failure(Error.Validation("TimeSlot.MaxParticipantsMustBePositive", "Max participants must be greater than zero."));

		return Result.Success();
	}
}
