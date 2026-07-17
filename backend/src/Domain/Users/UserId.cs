using Domain.Primitives;

namespace Domain.Users;

public readonly record struct UserId : IValueObject
{
	public Guid Value { get; }

	private UserId(Guid value) => Value = value;

	public static Result<UserId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<UserId>(Error.Validation("UserId.Empty", "UserId must not be empty."))
			: Result.Success(new UserId(value));

	public static UserId New() => new(Guid.CreateVersion7());
}
