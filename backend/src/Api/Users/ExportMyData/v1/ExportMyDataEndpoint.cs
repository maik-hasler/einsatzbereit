using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.ExportMyData.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.ExportMyData.v1;

internal sealed class ExportMyDataEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/me/export", ExportMyDataAsync)
			.WithName("ExportMyData")
			.WithTags("Users")
			.Produces<UserDataExportResponse>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> ExportMyDataAsync(
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem(
				"Unable to identify the current user.",
				statusCode: StatusCodes.Status401Unauthorized);
		}

		var result = await sender.Send(new ExportMyDataQuery(UserId.Create(userId).GetValueOrThrow()), cancellationToken);
		return Results.Ok(result);
	}
}
