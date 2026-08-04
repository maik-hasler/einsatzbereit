using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Notifications.DeleteReadNotifications.v1;

internal sealed class DeleteReadNotificationsCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DeleteReadNotificationsCommand, int>
{
	public async ValueTask<int> Handle(
		DeleteReadNotificationsCommand request,
		CancellationToken cancellationToken = default) =>
		await dbContext.DeleteReadNotificationsForRecipientAsync(request.RecipientId, cancellationToken);
}
