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

	// Captured at creation time so the notification text can still interpolate
	// a title once the related opportunity is gone (hard-deleted, or
	// shadow-deleted and filtered out by VolunteerOpportunityConfiguration's
	// query filter) by the time this notification is read - a live join
	// against the opportunity table alone finds nothing for either case
	// (einsatzbereit#2073). Null for notification kinds that never pass a
	// title through (e.g. invitation/feedback notifications resolve their
	// display text from a live join elsewhere and never go stale the same way).
	public string? TitleSnapshot { get; private set; }

	public bool IsRead { get; private set; }

	// Retention (NotificationRetentionJob) prunes read notifications relative to
	// when they were actually read, not CreatedOn - a notification read the
	// instant it arrived and one read 89 days later must not be pruned at the
	// same moment (#1725).
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
