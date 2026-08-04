using Api.Common.Authentication;
using Api.Common.RateLimiting;
using Api.Common.Endpoints;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.SaveSearchAlert.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.SaveSearchAlert.v1;

internal sealed class SaveSearchAlertEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/users/me/search-alert", SaveSearchAlertAsync)
			.WithName("SaveSearchAlert")
			.WithTags("Users")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> SaveSearchAlertAsync(
		[FromBody] SaveSearchAlertRequest request,
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
		}

		var command = new SaveSearchAlertCommand(
			UserId.Create(userId).GetValueOrThrow(),
			request.Occurrence,
			request.ParticipationType,
			request.IsRemote,
			request.CenterLatitude,
			request.CenterLongitude,
			request.RadiusKm,
			request.Categories,
			request.Tag);

		await sender.Send(command, cancellationToken);

		return Results.NoContent();
	}
}
