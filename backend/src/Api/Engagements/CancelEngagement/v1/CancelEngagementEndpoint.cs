using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements.CancelEngagement.v1;
using Domain.Engagements;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.CancelEngagement.v1;

internal sealed class CancelEngagementEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/engagements/{engagementId:guid}/cancel", CancelEngagementAsync)
			.WithName("CancelEngagement")
			.WithTags("Engagements")
			.Produces<EngagementStatusResponse>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> CancelEngagementAsync(
		[FromRoute] Guid engagementId,
		[FromBody] CancelEngagementRequest? body,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		try
		{
			var command = new CancelEngagementCommand(new EngagementId(engagementId), body?.Reason);
			var engagement = await sender.Send(command, cancellationToken);
			return Results.Ok(new EngagementStatusResponse(engagement.Id.Value, engagement.Status.ToString(), engagement.ModifiedOn, engagement.CancellationReason));
		}
		catch (DomainException ex) when (ex.Message.Contains("not found"))
		{
			return Results.NotFound();
		}
		catch (DomainException ex)
		{
			return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
		}
	}
}
