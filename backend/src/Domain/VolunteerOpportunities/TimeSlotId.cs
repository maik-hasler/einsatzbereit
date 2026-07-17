using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public readonly record struct TimeSlotId : IValueObject
{
	public Guid Value { get; }

	private TimeSlotId(Guid value) => Value = value;

	public static Result<TimeSlotId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<TimeSlotId>(Error.Validation("TimeSlotId.Empty", "TimeSlotId must not be empty."))
			: Result.Success(new TimeSlotId(value));

	public static TimeSlotId New() => new(Guid.CreateVersion7());
}
