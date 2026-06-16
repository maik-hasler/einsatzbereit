using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapPut("/volunteer-opportunities/{opportunityId:guid}", UpdateVolunteerOpportunityAsync)
			.WithName("UpdateVolunteerOpportunity")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Write)
			.MapToApiVersion(1);

	private static async Task<IResult> UpdateVolunteerOpportunityAsync(
		[FromRoute] Guid opportunityId,
		[FromBody] UpdateVolunteerOpportunityRequest request,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? new UserId(uid) : throw new DomainException("Invalid user.");
		Address? address = null;
		if (!request.IsRemote && !string.IsNullOrWhiteSpace(request.Street))
			address = new Address(
				request.Street ?? string.Empty,
				request.HouseNumber ?? string.Empty,
				request.ZipCode ?? string.Empty,
				request.City ?? string.Empty);

		if (!Enum.TryParse<Occurrence>(request.Occurrence, ignoreCase: true, out var occurrence))
		{
			return Results.Problem(
				"Invalid occurrence. Allowed values: OneTime, Recurring.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!Enum.TryParse<ParticipationType>(request.ParticipationType, ignoreCase: true, out var participationType))
		{
			return Results.Problem(
				"Invalid participation type. Allowed values: Waitlist, IndividualContact.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!Enum.TryParse<CheckInMethod>(request.CheckInMethod, ignoreCase: true, out var checkInMethod))
		{
			return Results.Problem(
				"Invalid check-in method. Allowed values: None, QRCode, PINCode, Manual.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		Category? category = null;
		if (!string.IsNullOrWhiteSpace(request.Category))
		{
			if (!Enum.TryParse<Category>(request.Category, ignoreCase: true, out var parsedCategory))
			{
				return Results.Problem(
					"Invalid category.",
					statusCode: StatusCodes.Status400BadRequest);
			}
			category = parsedCategory;
		}

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId,
			request.Title ?? string.Empty,
			request.Description ?? string.Empty,
			request.IsRemote,
			address,
			occurrence,
			participationType,
			checkInMethod,
			category,
			[.. request.Tags ?? []],
			userId);

		await sender.Send(command, cancellationToken);

		return Results.NoContent();
	}
}
