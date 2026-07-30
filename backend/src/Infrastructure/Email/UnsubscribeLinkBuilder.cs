using Application.Common.Email;
using Domain.Users;
using Infrastructure.Common;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

internal sealed class UnsubscribeLinkBuilder(
	IOptions<ApiOptions> options)
	: IUnsubscribeLinkBuilder
{
	public string Build(UserId userId, Guid unsubscribeToken, EmailNotificationType type) =>
		$"{options.Value.PublicBaseUrl.TrimEnd('/')}/v1/users/{userId.Value}/unsubscribe?type={type}&token={unsubscribeToken}";
}
