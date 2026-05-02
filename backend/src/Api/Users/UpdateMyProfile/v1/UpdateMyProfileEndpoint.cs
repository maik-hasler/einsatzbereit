using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Users.UpdateMyProfile.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.UpdateMyProfile.v1;

internal sealed class UpdateMyProfileEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/users/me", UpdateMyProfileAsync)
			.WithName("UpdateMyProfile")
			.WithTags("Users")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.MapToApiVersion(1);

	private static async Task<IResult> UpdateMyProfileAsync(
		[FromBody] UpdateMyProfileRequest request,
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
		}

		var command = new UpdateMyProfileCommand(
			new UserId(userId),
			request.FirstName,
			request.LastName);

		await sender.Send(command, cancellationToken);

		return Results.NoContent();
	}
}
