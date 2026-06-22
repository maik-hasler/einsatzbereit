using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Organizations.GetOrganizationCalendarEvents.v1;
using Domain.Primitives;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Organizations.GetOrganizationCalendarEvents.v1;

internal sealed class GetOrganizationCalendarEventsEndpoint
	: IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/organizations/{organizationId:guid}/calendar-events", GetOrganizationCalendarEventsAsync)
			.WithName("GetOrganizationCalendarEvents")
			.Produces<IReadOnlyList<OrganizationCalendarEventDto>>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);
	}

	private static async Task<IResult> GetOrganizationCalendarEventsAsync(
		[FromRoute] Guid organizationId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid)
			? new UserId(uid)
			: throw new DomainException("Invalid user.");

		var query = new GetOrganizationCalendarEventsQuery(organizationId, userId);
		var result = await sender.Send(query, cancellationToken);
		return Results.Ok(result);
	}
}
