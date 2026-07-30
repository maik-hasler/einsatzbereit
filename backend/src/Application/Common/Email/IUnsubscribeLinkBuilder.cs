using Domain.Users;

namespace Application.Common.Email;

public interface IUnsubscribeLinkBuilder
{
	string Build(UserId userId, Guid unsubscribeToken, EmailNotificationType type);
}
