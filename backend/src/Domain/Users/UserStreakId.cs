using Domain.Primitives;

namespace Domain.Users;

public readonly record struct UserStreakId : IValueObject
{
	public Guid Value { get; }

	private UserStreakId(Guid value) => Value = value;

	public static Result<UserStreakId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<UserStreakId>(Error.Validation("UserStreakId.Empty", "UserStreakId must not be empty."))
			: Result.Success(new UserStreakId(value));

	public static UserStreakId New() => new(Guid.CreateVersion7());
}
