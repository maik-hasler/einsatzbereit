using Domain.Primitives;
using Domain.Users;

namespace Domain.Notifications;

public sealed class Notification
	: AggregateRoot<NotificationId>,
		IAuditableEntity
{
	public UserId RecipientId { get; private set; }

	public NotificationKind Kind { get; private set; }

	public Guid RelatedEntityId { get; private set; }

	public string? TitleSnapshot { get; private set; }

	public bool IsRead { get; private set; }

	public DateTimeOffset? ReadOn { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private Notification() : base(default) { }
#pragma warning restore CS8618

	private Notification(
		NotificationId id,
		UserId recipientId,
		NotificationKind kind,
		Guid relatedEntityId,
		string? titleSnapshot)
		: base(id)
	{
		RecipientId = recipientId;
		Kind = kind;
		RelatedEntityId = relatedEntityId;
		TitleSnapshot = titleSnapshot;
		IsRead = false;
	}

	public static Notification Create(
		UserId recipientId,
		NotificationKind kind,
		Guid relatedEntityId,
		string? titleSnapshot = null) =>
		new(
			NotificationId.New(),
			recipientId,
			kind,
			relatedEntityId,
			titleSnapshot);

	public void MarkRead(DateTimeOffset readOn)
	{
		IsRead = true;
		ReadOn = readOn;
	}

	public void MarkUnread()
	{
		IsRead = false;
		ReadOn = null;
	}
}
