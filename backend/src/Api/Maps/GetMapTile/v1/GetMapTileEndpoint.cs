using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Maps.GetMapTile.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Maps.GetMapTile.v1;

// AllowAnonymous is deliberate: map tiles are rendered on public pages (e.g. an
// opportunity's location map) before/without a login, and Leaflet's TileLayer
// has no way to attach a Bearer token to its own tile requests. Proxying tiles
// through the backend (rather than the frontend calling tile.openstreetmap.org
// directly) keeps visitor IP addresses from reaching the OpenStreetMap
// Foundation's tile servers - see docs/ADRs/5_map_and_geocoding_request_proxying.adoc.
internal sealed class GetMapTileEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet("/maps/tiles/{zoom:int}/{x:int}/{y:int}.png", GetMapTileAsync)
			.WithName("GetMapTile")
			.Produces(StatusCodes.Status200OK, contentType: "image/png")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
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
