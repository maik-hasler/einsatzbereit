using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.GetVolunteerOpportunities.v1;

internal sealed class GetVolunteerOpportunitiesEndpoint
	: IEndpoint
{
	public void MapEndpoint(
		IEndpointRouteBuilder app)
	{
		app.MapGet("/volunteer-opportunities", GetVolunteerOpportunitiesAsync)
			.WithName("GetVolunteerOpportunities")
			.Produces<PagedList<VolunteerOpportunitySummary>>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> GetVolunteerOpportunitiesAsync(
		[AsParameters] GetVolunteerOpportunitiesRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		if (request.PageNumber < 1)
			return Results.Problem("PageNumber must be at least 1.", statusCode: StatusCodes.Status400BadRequest);

		if (request.PageSize < 1 || request.PageSize > 100)
			return Results.Problem("PageSize must be between 1 and 100.", statusCode: StatusCodes.Status400BadRequest);

		var hasLat = request.CenterLatitude.HasValue;
		var hasLng = request.CenterLongitude.HasValue;
		if (hasLat != hasLng)
			return Results.Problem("CenterLatitude and CenterLongitude must both be supplied together.", statusCode: StatusCodes.Status400BadRequest);

		if (request.CenterLatitude is < -90 or > 90)
			return Results.Problem("CenterLatitude must be between -90 and 90.", statusCode: StatusCodes.Status400BadRequest);

		if (request.CenterLongitude is < -180 or > 180)
			return Results.Problem("CenterLongitude must be between -180 and 180.", statusCode: StatusCodes.Status400BadRequest);

		if (request.RadiusKm is <= 0)
			return Results.Problem("RadiusKm must be greater than zero.", statusCode: StatusCodes.Status400BadRequest);

		if (!string.IsNullOrWhiteSpace(request.Occurrence)
			&& (!Enum.TryParse<Occurrence>(request.Occurrence, ignoreCase: true, out var occurrence) || !Enum.IsDefined(occurrence)))
		{
			return Results.Problem(
				"Invalid occurrence. Allowed values: OneTime, Recurring.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!string.IsNullOrWhiteSpace(request.ParticipationType)
			&& (!Enum.TryParse<ParticipationType>(request.ParticipationType, ignoreCase: true, out var participationType) || !Enum.IsDefined(participationType)))
		{
			return Results.Problem(
				"Invalid participation type. Allowed values: Waitlist, IndividualContact.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (request.Categories is { Length: > 0 }
			&& request.Categories.Any(c => !Enum.TryParse<Category>(c, ignoreCase: true, out var category) || !Enum.IsDefined(category)))
		{
			return Results.Problem(
				"Invalid category. Allowed values: Social, Environment, Sport, Education, DisasterRelief, Health, Animals, Culture, Technology, Other.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		var query = new GetVolunteerOpportunitiesQuery(
			request.PageNumber,
			request.PageSize,
			request.City,
			request.Occurrence,
			request.ParticipationType,
			request.IsRemote,
			request.DateFrom,
			request.DateTo,
			request.North,
			request.South,
			request.East,
			request.West,
			request.CenterLatitude,
			request.CenterLongitude,
			request.RadiusKm,
			request.Categories,
			request.Tag);

		var result = await sender.Send(query, cancellationToken);

		return Results.Ok(result);
	}
}
