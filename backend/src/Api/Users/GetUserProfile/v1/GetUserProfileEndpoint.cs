using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Users.GetUserProfile.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.GetUserProfile.v1;

internal sealed class GetUserProfileEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/me", GetUserProfileAsync)
			.WithName("GetUserProfile")
			.WithTags("Users")
			.Produces<MyProfileResponse>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.MapToApiVersion(1);

	private static async Task<IResult> GetUserProfileAsync(
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
		}

		var result = await sender.Send(new GetUserProfileQuery(new UserId(userId)), cancellationToken);
		return Results.Ok(result);
	}
}
