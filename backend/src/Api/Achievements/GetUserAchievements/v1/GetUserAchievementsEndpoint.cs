using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Achievements;
using Application.Achievements.GetUserAchievements.v1;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Achievements.GetUserAchievements.v1;

internal sealed class GetUserAchievementsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/users/{userId:guid}/achievements", GetUserAchievementsAsync)
			.WithName("GetUserAchievements")
			.WithTags("Achievements")
			.Produces<List<AchievementSummary>>()
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetUserAchievementsAsync(
		Guid userId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var query = new GetUserAchievementsQuery(UserId.Create(userId).GetValueOrThrow());
		var result = await sender.Send(query, cancellationToken);
		return Results.Ok(result);
	}
}
