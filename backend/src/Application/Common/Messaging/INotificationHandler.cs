using Domain.Primitives;

namespace Application.Common.Messaging;

public interface INotificationHandler<in TNotification>
	where TNotification : INotification
{
	Task Handle(
		TNotification notification,
		CancellationToken cancellationToken);
}
