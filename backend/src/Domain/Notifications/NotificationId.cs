using Domain.Primitives;

namespace Domain.Notifications;

public readonly record struct NotificationId : IValueObject
{
	public Guid Value { get; }

	private NotificationId(Guid value) => Value = value;

	public static Result<NotificationId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<NotificationId>(Error.Validation("NotificationId.Empty", "NotificationId must not be empty."))
			: Result.Success(new NotificationId(value));

	public static NotificationId New() => new(Guid.CreateVersion7());
}
