using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.GetOrganizationDashboard.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Organizations.GetOrganizationDashboard.v1;

internal sealed class GetOrganizationDashboardEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/organizations/{organizationId:guid}/dashboard", GetOrganizationDashboardAsync)
			.WithName("GetOrganizationDashboard")
			.Produces<OrganizationDashboardResponse>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> GetOrganizationDashboardAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? new UserId(uid) : throw new DomainException("Invalid user.");
		var query = new GetOrganizationDashboardQuery(organizationId, userId);

		var result = await sender.Send(query, cancellationToken);

		return result is null ? Results.NotFound() : Results.Ok(result);
	}
}
