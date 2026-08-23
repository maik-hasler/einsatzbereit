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
		$"{options.Value.FrontendBaseUrl}/unsubscribe?userId={userId.Value}&type={type}&token={unsubscribeToken}";
}
