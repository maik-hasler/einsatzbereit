using Application.Common.Email;
using Domain.Users;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Email;

// Points at a frontend confirmation page rather than the backend endpoint
// directly (#1725) - the backend's unsubscribe endpoint is a state-changing
// POST, which a plain-text email link can't trigger by itself; the frontend
// page only calls it once the recipient explicitly clicks a confirm button,
// so a mail scanner or link prefetcher merely loading this URL can no longer
// silently opt anyone out. Reuses the same Cors:Origins-derived frontend base
// URL as GetSitemapEndpoint/UnsubscribeEndpoint's former redirect, since
// there's no dedicated "frontend base URL" setting in this codebase.
internal sealed class UnsubscribeLinkBuilder(
	IConfiguration configuration)
	: IUnsubscribeLinkBuilder
{
	public string Build(UserId userId, Guid unsubscribeToken, EmailNotificationType type)
	{
		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var frontendBaseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";

		return $"{frontendBaseUrl}/unsubscribe?userId={userId.Value}&type={type}&token={unsubscribeToken}";
	}
}
