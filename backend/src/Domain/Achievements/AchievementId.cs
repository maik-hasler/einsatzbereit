using Domain.Primitives;

namespace Domain.Achievements;

public readonly record struct AchievementId : IValueObject
{
	public Guid Value { get; }

	private AchievementId(Guid value) => Value = value;

	public static Result<AchievementId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<AchievementId>(Error.Validation("AchievementId.Empty", "AchievementId must not be empty."))
			: Result.Success(new AchievementId(value));

	public static AchievementId New() => new(Guid.CreateVersion7());
}
