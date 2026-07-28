using Application.Common.Messaging;

namespace Application.Notifications.GetUnreadNotificationCount.v1;

internal sealed class GetUnreadNotificationCountQueryHandler(
	INotificationReadRepository readRepository)
	: IQueryHandler<GetUnreadNotificationCountQuery, int>
{
	public async ValueTask<int> Handle(
		GetUnreadNotificationCountQuery request,
		CancellationToken cancellationToken = default) =>
			await readRepository.CountUnreadByRecipientAsync(request.RecipientId, cancellationToken);
}
