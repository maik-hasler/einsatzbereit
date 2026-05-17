using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Notifications.MarkAllNotificationsRead.v1;

internal sealed class MarkAllNotificationsReadCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<MarkAllNotificationsReadCommand, int>
{
	public async ValueTask<int> Handle(
		MarkAllNotificationsReadCommand request,
		CancellationToken cancellationToken = default)
	{
		var unread = await dbContext.GetUnreadNotificationsForRecipientAsync(
			request.RecipientId, cancellationToken);

		foreach (var n in unread)
			n.MarkRead();

		return unread.Count;
	}
}
