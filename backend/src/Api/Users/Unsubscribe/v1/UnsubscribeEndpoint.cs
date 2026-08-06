using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.Unsubscribe.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.Unsubscribe.v1;

// One-click unsubscribe link embedded in transactional emails (#1055) - must work
// for a recipient who never signed in, so this is intentionally unauthenticated
// and identifies the target solely via the opaque per-user UnsubscribeToken.
internal sealed class UnsubscribeEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/{userId:guid}/unsubscribe", UnsubscribeAsync)
			.WithName("Unsubscribe")
			.WithTags("Users")
			.Produces(StatusCodes.Status302Found)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	// Redirects into a branded, localized frontend route rather than returning raw
	// HTML directly (#1675) - this endpoint has no locale of its own to render in,
	// so the frontend's own i18n (German-default) takes over from here. Reuses the
	// same Cors:Origins-derived frontend base URL as GetSitemapEndpoint/
	// GetEngagementCalendarEndpoint, since there's no dedicated "frontend base URL"
	// setting in this codebase.
	private static async Task<IResult> UnsubscribeAsync(
		[FromRoute] Guid userId,
		[FromQuery] string type,
		[FromQuery] Guid token,
		[FromServices] ISender sender,
		[FromServices] IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		if (!Enum.TryParse<EmailNotificationType>(type, out var notificationType))
		{
			return Results.Problem("Unknown notification type.", statusCode: StatusCodes.Status400BadRequest);
		}

		await sender.Send(
			new UnsubscribeCommand(UserId.Create(userId).GetValueOrThrow(), token, notificationType),
			cancellationToken);

		var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
		var frontendBaseUrl = origins.Length > 0 ? origins[0].TrimEnd('/') : "";

		return Results.Redirect($"{frontendBaseUrl}/unsubscribed?type={Uri.EscapeDataString(type)}");
	}
}
