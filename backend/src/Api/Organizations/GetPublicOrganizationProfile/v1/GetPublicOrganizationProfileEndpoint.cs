using Api.Common.Endpoints;
using Api.Common.OutputCaching;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.GetPublicOrganizationProfile.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Organizations.GetPublicOrganizationProfile.v1;

internal sealed class GetPublicOrganizationProfileEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/organizations/{organizationId:guid}/profile", GetPublicOrganizationProfileAsync)
			.WithName("GetPublicOrganizationProfile")
			.Produces<PublicOrganizationProfileResponse>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.CacheOutput(OutputCachingPolicies.ShortPublicRead)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> GetPublicOrganizationProfileAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var query = new GetPublicOrganizationProfileQuery(organizationId);
		var result = await sender.Send(query, cancellationToken);
		return result is null ? Results.NotFound() : Results.Ok(result);
	}
}
