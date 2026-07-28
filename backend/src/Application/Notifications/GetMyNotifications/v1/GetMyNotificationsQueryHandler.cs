using Application.Common.Messaging;

namespace Application.Notifications.GetMyNotifications.v1;

internal sealed class GetMyNotificationsQueryHandler(
	INotificationReadRepository readRepository)
	: IQueryHandler<GetMyNotificationsQuery, NotificationsPage>
{
	private const int PageSize = 50;

	public async ValueTask<NotificationsPage> Handle(
		GetMyNotificationsQuery request,
		CancellationToken cancellationToken = default)
	{
		var notifications = await readRepository.GetByRecipientAsync(
			request.RecipientId, request.Before, request.BeforeId, PageSize + 1, cancellationToken);

		var hasMore = notifications.Count > PageSize;
		var items = hasMore ? notifications.Take(PageSize).ToList() : notifications;

		return new NotificationsPage(items, hasMore);
	}
}
