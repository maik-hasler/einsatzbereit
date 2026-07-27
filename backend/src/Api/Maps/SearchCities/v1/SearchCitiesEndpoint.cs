using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Geocoding;
using Application.Common.Messaging;
using Application.Maps.SearchCities.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Maps.SearchCities.v1;

internal sealed class SearchCitiesEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/maps/cities", SearchCitiesAsync)
			.WithName("SearchCities")
			.Produces<IReadOnlyList<CitySuggestion>>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> SearchCitiesAsync(
		[AsParameters] SearchCitiesRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Q))
			return Results.Ok(Array.Empty<CitySuggestion>());

		var query = new SearchCitiesQuery(request.Q);

		var result = await sender.Send(query, cancellationToken);

		return Results.Ok(result);
	}
}
