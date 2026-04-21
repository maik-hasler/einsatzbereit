using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Engagements;
using Application.Engagements.GetEngagements.v1;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.GetEngagements.v1;

internal sealed class GetEngagementsEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/volunteer-opportunities/{opportunityId:guid}/engagements", GetEngagementsAsync)
            .WithName("GetEngagements")
            .WithTags("Engagements")
            .Produces<List<EngagementSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> GetEngagementsAsync(
        [FromRoute] Guid opportunityId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetEngagementsQuery(new VolunteerOpportunityId(opportunityId));
        var result = await sender.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
