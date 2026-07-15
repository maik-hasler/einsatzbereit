using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.Engagements;
using Application.VolunteerOpportunities.GetOpportunityFeedback.v1;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.VolunteerOpportunities.GetOpportunityFeedback.v1;

internal sealed class GetOpportunityFeedbackEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet(
				"/volunteer-opportunities/{opportunityId:guid}/feedback",
				GetFeedbackAsync)
			.WithName("GetOpportunityFeedback")
			.WithTags("VolunteerOpportunities")
			.Produces<OpportunityFeedbackSummary>()
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetFeedbackAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? new UserId(uid) : throw new DomainException("Invalid user.");

		var result = await sender.Send(
			new GetOpportunityFeedbackQuery(new VolunteerOpportunityId(opportunityId), userId),
			cancellationToken);

		return Results.Ok(result);
	}
}
