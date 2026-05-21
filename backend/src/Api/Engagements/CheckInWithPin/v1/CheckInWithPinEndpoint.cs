using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements.CheckInWithPin.v1;
using Domain.Engagements;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.CheckInWithPin.v1;

internal sealed class CheckInWithPinEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/me/engagements/{engagementId:guid}/check-in", CheckInWithPinAsync)
			.WithName("CheckInWithPin")
			.WithTags("Engagements")
			.Produces<EngagementStatusResponse>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> CheckInWithPinAsync(
		[FromRoute] Guid engagementId,
		[FromBody] CheckInWithPinRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var command = new CheckInWithPinCommand(new EngagementId(engagementId), request.Pin);
		var engagement = await sender.Send(command, cancellationToken);
		return Results.Ok(new EngagementStatusResponse(engagement.Id.Value, engagement.Status.ToString(), engagement.ModifiedOn));
	}
}
