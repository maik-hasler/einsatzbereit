using Api.Common.Authentication;
using Api.Common.RateLimiting;
using Api.Common.Endpoints;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.GetSearchAlert.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.GetSearchAlert.v1;

internal sealed class GetSearchAlertEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/me/search-alert", GetSearchAlertAsync)
			.WithName("GetSearchAlert")
			.WithTags("Users")
			.Produces<SearchAlertResponse>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetSearchAlertAsync(
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
		}

		var result = await sender.Send(
			new GetSearchAlertQuery(UserId.Create(userId).GetValueOrThrow()),
			cancellationToken);

		return Results.Ok(result);
	}
}
