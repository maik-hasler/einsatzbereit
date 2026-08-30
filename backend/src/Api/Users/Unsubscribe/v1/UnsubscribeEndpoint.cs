using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.Unsubscribe.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.Unsubscribe.v1;

internal sealed class UnsubscribeEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/{userId:guid}/unsubscribe", UnsubscribeAsync)
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
			throw new ResultFailureException(Error.Validation(
				"User.UnknownNotificationType",
				"Unknown notification type."));
		}

		await sender.Send(
			new UnsubscribeCommand(UserId.Create(userId).GetValueOrThrow(), token, notificationType),
			cancellationToken);

		// 204, not a redirect to the frontend: the confirm page calls this from
		// inside the SPA and routes itself on the outcome, so a failure stays a
		// translated message in the app rather than raw JSON on the API origin
		// that the user has no way back from (#2320).
		return Results.NoContent();
	}
}
