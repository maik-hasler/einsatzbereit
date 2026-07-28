namespace Application.Notifications;

public sealed record NotificationsPage(
	IReadOnlyList<NotificationSummary> Items,
	bool HasMore);
