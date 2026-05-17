namespace Application.Notifications;

public sealed record NotificationSummary(
	Guid Id,
	string Kind,
	Guid RelatedEntityId,
	bool IsRead,
	DateTimeOffset CreatedOn);
