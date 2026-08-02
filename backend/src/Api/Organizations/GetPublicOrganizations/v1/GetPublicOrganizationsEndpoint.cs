using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Organizations.GetPublicOrganizations.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Organizations.GetPublicOrganizations.v1;

internal sealed class GetPublicOrganizationsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/organizations/directory", GetPublicOrganizationsAsync)
			.WithName("GetPublicOrganizations")
			.Produces<PagedList<PublicOrganizationSummary>>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.ShortPublicRead)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> GetPublicOrganizationsAsync(
		[AsParameters] GetPublicOrganizationsRequest request,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		if (request.PageNumber < 1)
			return Results.Problem("PageNumber must be at least 1.", statusCode: StatusCodes.Status400BadRequest);

		if (request.PageSize < 1 || request.PageSize > 100)
			return Results.Problem("PageSize must be between 1 and 100.", statusCode: StatusCodes.Status400BadRequest);

		var query = new GetPublicOrganizationsQuery(
			request.PageNumber,
			request.PageSize,
			request.Search);

		var result = await sender.Send(query, cancellationToken);

		return Results.Ok(result);
	}
}
