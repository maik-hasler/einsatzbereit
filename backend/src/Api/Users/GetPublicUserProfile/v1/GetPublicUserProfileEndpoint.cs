using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Users.GetPublicUserProfile.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Users.GetPublicUserProfile.v1;

internal sealed class GetPublicUserProfileEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/{userId:guid}/public-profile", GetPublicUserProfileAsync)
			.WithName("GetPublicUserProfile")
			.WithTags("Users")
			.Produces<PublicUserProfileResponse>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetPublicUserProfileAsync(
		[FromRoute] Guid userId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var result = await sender.Send(
			new GetPublicUserProfileQuery(UserId.Create(userId).GetValueOrThrow()),
			cancellationToken);

		return result is null ? Results.NotFound() : Results.Ok(result);
	}
}
