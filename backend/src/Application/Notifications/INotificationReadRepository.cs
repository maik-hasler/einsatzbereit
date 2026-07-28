using Domain.Users;

namespace Application.Notifications;

public interface INotificationReadRepository
{
	ValueTask<List<NotificationSummary>> GetByRecipientAsync(
		UserId recipientId,
		DateTimeOffset? before,
		Guid? beforeId,
		int limit,
		CancellationToken cancellationToken = default);

	ValueTask<int> CountUnreadByRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default);
}
