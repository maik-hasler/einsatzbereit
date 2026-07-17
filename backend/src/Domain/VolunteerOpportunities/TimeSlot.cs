using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed class TimeSlot : Entity<TimeSlotId>
{
	public DateTimeOffset StartDateTime { get; private set; }

	public DateTimeOffset EndDateTime { get; private set; }

	public int MaxParticipants { get; private set; }

#pragma warning disable CS8618
	private TimeSlot() : base(default) { }
#pragma warning restore CS8618

	private TimeSlot(
		TimeSlotId id,
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int maxParticipants)
		: base(id)
	{
		StartDateTime = startDateTime;
		EndDateTime = endDateTime;
		MaxParticipants = maxParticipants;
	}

	public static Result<TimeSlot> Create(
		DateTimeOffset startDateTime,
		DateTimeOffset endDateTime,
		int maxParticipants,
		DateTimeOffset now)
	{
		var validation = Validate(startDateTime, endDateTime, maxParticipants, now);
		if (validation.IsFailure)
			return Result.Failure<TimeSlot>(validation.Error);

		return new TimeSlot(
			TimeSlotId.New(),
			startDateTime,
			endDateTime,
			maxParticipants);
	}

	public Result Update(DateTimeOffset startDateTime, DateTimeOffset endDateTime, int maxParticipants, DateTimeOffset now)
	{
		var validation = Validate(startDateTime, endDateTime, maxParticipants, now);
		if (validation.IsFailure)
			return validation;

		StartDateTime = startDateTime;
		EndDateTime = endDateTime;
		MaxParticipants = maxParticipants;
		return Result.Success();
	}

	private static Result Validate(DateTimeOffset startDateTime, DateTimeOffset endDateTime, int maxParticipants, DateTimeOffset now)
	{
		if (startDateTime <= now)
			return Result.Failure(Error.Validation("TimeSlot.StartMustBeFuture", "Start date must be in the future."));

		if (endDateTime <= startDateTime)
			return Result.Failure(Error.Validation("TimeSlot.EndMustBeAfterStart", "End date must be after start date."));

		if (maxParticipants <= 0)
			return Result.Failure(Error.Validation("TimeSlot.MaxParticipantsMustBePositive", "Max participants must be greater than zero."));

		return Result.Success();
	}
}
