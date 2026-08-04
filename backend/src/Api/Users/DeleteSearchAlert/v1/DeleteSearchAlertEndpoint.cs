using Api.Common.Authentication;
using Api.Common.RateLimiting;
using Api.Common.Endpoints;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.DeleteSearchAlert.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.DeleteSearchAlert.v1;

internal sealed class DeleteSearchAlertEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapDelete("/users/me/search-alert", DeleteSearchAlertAsync)
			.WithName("DeleteSearchAlert")
			.WithTags("Users")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> DeleteSearchAlertAsync(
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
		}

		await sender.Send(
			new DeleteSearchAlertCommand(UserId.Create(userId).GetValueOrThrow()),
			cancellationToken);

		return Results.NoContent();
	}
}
