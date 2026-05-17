using Application.Common.Messaging;

namespace Application.Notifications.GetMyNotifications.v1;

internal sealed class GetMyNotificationsQueryHandler(
	INotificationReadRepository readRepository)
	: IQueryHandler<GetMyNotificationsQuery, List<NotificationSummary>>
{
	public async ValueTask<List<NotificationSummary>> Handle(
		GetMyNotificationsQuery request,
		CancellationToken cancellationToken = default) =>
			await readRepository.GetByRecipientAsync(request.RecipientId, cancellationToken);
}
