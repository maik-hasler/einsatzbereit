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
//
// POST, not GET (#1725): the email footer links here through a frontend
// confirmation page (UnsubscribeLinkBuilder builds that URL, not this one
// directly) that only calls this endpoint once the recipient explicitly
// clicks a confirm button. A GET here would let a mail scanner or link
// prefetcher silently trigger the opt-out just by following the link in the
// email body, without the recipient ever choosing to leave.
internal sealed class UnsubscribeEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/users/{userId:guid}/unsubscribe", UnsubscribeAsync)
			.WithName("Unsubscribe")
			.WithTags("Users")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> UnsubscribeAsync(
		[FromRoute] Guid userId,
		[FromQuery] string type,
		[FromQuery] Guid token,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		if (!Enum.TryParse<EmailNotificationType>(type, out var notificationType))
		{
			return Results.Problem("Unknown notification type.", statusCode: StatusCodes.Status400BadRequest);
		}

		await sender.Send(
			new UnsubscribeCommand(UserId.Create(userId).GetValueOrThrow(), token, notificationType),
			cancellationToken);

		return Results.NoContent();
	}
}
