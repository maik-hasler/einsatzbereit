using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.CreateTimeSlot.v1;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.CreateTimeSlot.v1;

internal sealed class CreateTimeSlotEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPost("/volunteer-opportunities/{opportunityId:guid}/time-slots", CreateTimeSlotAsync)
			.WithName("CreateTimeSlot")
			.Produces<CreateTimeSlotResponse>(StatusCodes.Status201Created)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> CreateTimeSlotAsync(
		[FromRoute] Guid opportunityId,
		[FromBody] CreateTimeSlotRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		try
		{
			var command = new CreateTimeSlotCommand(opportunityId, request.StartDateTime, request.EndDateTime, request.MaxParticipants);
			var timeSlotId = await sender.Send(command, cancellationToken);
			var response = new CreateTimeSlotResponse(timeSlotId, request.StartDateTime, request.EndDateTime, request.MaxParticipants);
			return Results.Created($"/v1/volunteer-opportunities/{opportunityId}/time-slots/{timeSlotId}", response);
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
