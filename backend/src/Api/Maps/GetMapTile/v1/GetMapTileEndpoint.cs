using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Maps.GetMapTile.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Maps.GetMapTile.v1;

internal sealed class GetMapTileEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/maps/tiles/{zoom:int}/{x:int}/{y:int}.png", GetMapTileAsync)
			.WithName("GetMapTile")
			.Produces(StatusCodes.Status200OK, contentType: "image/png")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.MapTiles)
			.MapToApiVersion(1);

	private static async Task<IResult> GetMapTileAsync(
		[FromRoute] int zoom,
		[FromRoute] int x,
		[FromRoute] int y,
		[FromServices] ISender sender,
		HttpContext httpContext,
		CancellationToken cancellationToken)
	{
		var tile = await sender.Send(new GetMapTileQuery(zoom, x, y), cancellationToken);

		if (tile is null)
			return Results.NotFound();

		httpContext.Response.Headers.CacheControl = "public, max-age=86400";

		return Results.File(tile.Content, contentType: tile.ContentType);
	}
}
