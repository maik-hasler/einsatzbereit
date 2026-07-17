using Domain.Primitives;

namespace Domain.Engagements;

public readonly record struct EngagementId : IValueObject
{
	public Guid Value { get; }

	private EngagementId(Guid value) => Value = value;

	public static Result<EngagementId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<EngagementId>(Error.Validation("EngagementId.Empty", "EngagementId must not be empty."))
			: Result.Success(new EngagementId(value));

	public static EngagementId New() => new(Guid.CreateVersion7());
}
