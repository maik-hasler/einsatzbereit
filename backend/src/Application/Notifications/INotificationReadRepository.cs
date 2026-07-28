using Domain.Users;

namespace Application.Notifications;

public interface INotificationReadRepository
{
	ValueTask<List<NotificationSummary>> GetByRecipientAsync(
		UserId recipientId,
		DateTimeOffset? before,
		int limit,
		CancellationToken cancellationToken = default);

	ValueTask<int> CountUnreadByRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default);
}
