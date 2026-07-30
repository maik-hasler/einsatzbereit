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
			.Produces(StatusCodes.Status200OK, contentType: "text/html")
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

		return Results.Content(
			"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Unsubscribed</title></head>" +
			"<body><p>You have been unsubscribed from this type of email. You can re-enable it any time " +
			"from your notification preferences in your profile.</p></body></html>",
			"text/html");
	}
}
