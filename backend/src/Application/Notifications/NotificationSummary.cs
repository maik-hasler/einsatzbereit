namespace Application.Notifications;

public sealed record NotificationSummary(
	Guid Id,
	string Kind,
	Guid RelatedEntityId,
	string? RelatedTitle,
	string? ActionUrl,
	bool IsRead,
	DateTimeOffset CreatedOn);
