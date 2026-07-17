using Domain.Primitives;

namespace Application.Common.Messaging;

public interface IPublisher
{
	Task Publish(
		INotification notification,
		CancellationToken cancellationToken = default);
}
