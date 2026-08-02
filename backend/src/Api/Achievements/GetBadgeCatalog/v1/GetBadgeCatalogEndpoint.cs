using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Achievements.BadgeCatalog;
using Application.Achievements.GetBadgeCatalog.v1;
using Application.Common.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Achievements.GetBadgeCatalog.v1;

internal sealed class GetBadgeCatalogEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/badges", GetBadgeCatalogAsync)
			.WithName("GetBadgeCatalog")
			.WithTags("Achievements")
			.Produces<List<BadgeCatalogEntry>>()
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.LongPublicRead)
			.MapToApiVersion(1);

	private static async Task<IResult> GetBadgeCatalogAsync(
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var result = await sender.Send(new GetBadgeCatalogQuery(), cancellationToken);
		return Results.Ok(result);
	}
}
