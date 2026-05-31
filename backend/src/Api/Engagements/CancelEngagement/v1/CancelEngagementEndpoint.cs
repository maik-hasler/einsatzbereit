using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements.CancelEngagement.v1;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		try
		{
			var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? new UserId(uid) : throw new DomainException("Invalid user.");
			var command = new CancelEngagementCommand(new EngagementId(engagementId), userId, body?.Reason);
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
