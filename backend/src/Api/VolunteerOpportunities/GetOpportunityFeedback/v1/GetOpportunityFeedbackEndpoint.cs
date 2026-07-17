using Api.Common.Authentication;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Engagements;
using Application.VolunteerOpportunities.GetOpportunityFeedback.v1;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

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
		CancellationToken cancellationToken)
	{
		var result = await sender.Send(
			new GetOpportunityFeedbackQuery(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow()),
			cancellationToken);

		return Results.Ok(result);
	}
}
