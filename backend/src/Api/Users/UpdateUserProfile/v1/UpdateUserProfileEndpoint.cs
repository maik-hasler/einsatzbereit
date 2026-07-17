using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.UpdateUserProfile.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.UpdateUserProfile.v1;

internal sealed class UpdateUserProfileEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/users/me", UpdateUserProfileAsync)
			.WithName("UpdateUserProfile")
			.WithTags("Users")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> UpdateUserProfileAsync(
		[FromBody] UpdateUserProfileRequest request,
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var subClaim = httpContext.User.FindFirst("sub")?.Value;
		if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
		{
			return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
		}

		if (request.FirstName is { Length: > 100 })
			return Results.Problem("FirstName must not exceed 100 characters.", statusCode: StatusCodes.Status400BadRequest);

		if (request.LastName is { Length: > 100 })
			return Results.Problem("LastName must not exceed 100 characters.", statusCode: StatusCodes.Status400BadRequest);

		if (request.Bio is { Length: > 1000 })
			return Results.Problem("Bio must not exceed 1000 characters.", statusCode: StatusCodes.Status400BadRequest);

		Domain.Users.PreferredContact? preferredContact = null;
		if (request.PreferredContact is not null &&
			Enum.TryParse<Domain.Users.PreferredContact>(request.PreferredContact, out var parsed))
		{
			preferredContact = parsed;
		}

		var command = new UpdateUserProfileCommand(
			UserId.Create(userId).GetValueOrThrow(),
			request.FirstName,
			request.LastName,
			request.Bio,
			request.Skills ?? [],
			request.Languages ?? [],
			preferredContact);

		await sender.Send(command, cancellationToken);

		return Results.NoContent();
	}
}
