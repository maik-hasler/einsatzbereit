namespace Application.Notifications;

public sealed record NotificationSummary(
	Guid Id,
	string Kind,
	string? RelatedTitle,
	string? ActionUrl,
	bool IsRead,
	DateTimeOffset CreatedOn);
