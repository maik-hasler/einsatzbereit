using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
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
