using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Meta.GetVersion.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Meta.GetVersion.v1;

internal sealed class GetVersionEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/meta/version", GetVersionAsync)
			.WithName("GetVersion")
			.WithTags("Meta")
			.Produces<string>()
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.LongPublicRead)
			.MapToApiVersion(1);

	private static async Task<IResult> GetVersionAsync(
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var version = await sender.Send(new GetVersionQuery(), cancellationToken);
		return Results.Ok(version);
	}
}
