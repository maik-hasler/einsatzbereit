using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;

internal sealed class DeleteVolunteerOpportunityEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/volunteer-opportunities/{opportunityId:guid}", DeleteVolunteerOpportunityAsync)
            .WithName("DeleteVolunteerOpportunity")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> DeleteVolunteerOpportunityAsync(
        [FromRoute] Guid opportunityId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new DeleteVolunteerOpportunityCommand(opportunityId);
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        }
        catch (DomainException ex) when (ex.Message.Contains("not found"))
        {
            return Results.NotFound();
        }
    }
}
