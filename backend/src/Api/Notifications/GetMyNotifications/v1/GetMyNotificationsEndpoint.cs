using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Notifications;
using Application.Notifications.GetMyNotifications.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Notifications.GetMyNotifications.v1;

internal sealed class GetMyNotificationsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/notifications", GetMyNotificationsAsync)
			.WithName("GetMyNotifications")
			.WithTags("Notifications")
			.Produces<NotificationsPage>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetMyNotificationsAsync(
		[FromServices] ISender sender,
		HttpContext httpContext,
		[FromQuery] DateTimeOffset? before,
		[FromQuery] Guid? beforeId,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);

		var query = new GetMyNotificationsQuery(UserId.Create(userId).GetValueOrThrow(), before, beforeId);
		var result = await sender.Send(query, cancellationToken);
		return Results.Ok(result);
	}
}
