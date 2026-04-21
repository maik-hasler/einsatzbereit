using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/volunteer-opportunities/{opportunityId:guid}", UpdateVolunteerOpportunityAsync)
            .WithName("UpdateVolunteerOpportunity")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> UpdateVolunteerOpportunityAsync(
        [FromRoute] Guid opportunityId,
        [FromBody] UpdateVolunteerOpportunityRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        Address? address = null;
        if (!request.IsRemote)
        {
            if (string.IsNullOrWhiteSpace(request.Street) || string.IsNullOrWhiteSpace(request.HouseNumber) ||
                string.IsNullOrWhiteSpace(request.ZipCode) || string.IsNullOrWhiteSpace(request.City))
            {
                return Results.Problem(
                    "Street, HouseNumber, ZipCode and City are required for non-remote opportunities.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            address = new Address(request.Street, request.HouseNumber, request.ZipCode, request.City);
        }

        var command = new UpdateVolunteerOpportunityCommand(opportunityId, request.Title, request.Description, request.IsRemote, address);

        await sender.Send(command, cancellationToken);

        return Results.NoContent();
    }
}
