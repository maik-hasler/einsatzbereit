using Domain.Users;

namespace Application.Notifications;

public interface INotificationReadRepository
{
	ValueTask<List<NotificationSummary>> GetByRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default);
}
