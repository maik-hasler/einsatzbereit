using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.DeleteTimeSlot.v1;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.DeleteTimeSlot.v1;

internal sealed class DeleteTimeSlotEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapDelete("/volunteer-opportunities/{opportunityId:guid}/time-slots/{timeSlotId:guid}", DeleteTimeSlotAsync)
			.WithName("DeleteTimeSlot")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> DeleteTimeSlotAsync(
		[FromRoute] Guid opportunityId,
		[FromRoute] Guid timeSlotId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		try
		{
			var command = new DeleteTimeSlotCommand(opportunityId, timeSlotId);
			await sender.Send(command, cancellationToken);
			return Results.NoContent();
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
