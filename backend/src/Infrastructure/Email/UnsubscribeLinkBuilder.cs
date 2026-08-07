using Application.Common.Email;
using Domain.Users;
using Infrastructure.Common;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

internal sealed class UnsubscribeLinkBuilder(
	IOptions<ApiOptions> options)
	: IUnsubscribeLinkBuilder
{
	// Points at a frontend confirmation page rather than directly at the backend's
	// state-changing GET endpoint (#1725) - a mail scanner or link prefetcher that
	// follows this link only ever loads that page (no state change); the actual
	// unsubscribe only happens once a person deliberately clicks "Confirm" on it,
	// which navigates on to the backend endpoint itself (unchanged, still
	// UnsubscribeEndpoint.cs's GET /v1/users/{userId}/unsubscribe).
	public string Build(UserId userId, Guid unsubscribeToken, EmailNotificationType type) =>
		$"{options.Value.FrontendBaseUrl}/unsubscribe?userId={userId.Value}&type={type}&token={unsubscribeToken}";
}
