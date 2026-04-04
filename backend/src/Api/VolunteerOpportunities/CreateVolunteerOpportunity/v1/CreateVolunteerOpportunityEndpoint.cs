using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

internal sealed class CreateVolunteerOpportunityEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/volunteer-opportunities", CreateVolunteerOpportunityAsync)
            .WithName("CreateVolunteerOpportunity")
            .Produces<CreateVolunteerOpportunityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> CreateVolunteerOpportunityAsync(
        [FromBody] CreateVolunteerOpportunityRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Occurrence>(request.Occurrence, ignoreCase: true, out var occurrence))
        {
            return Results.Problem(
                "Invalid occurrence. Allowed values: OneTime, Recurring.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<ParticipationType>(request.ParticipationType, ignoreCase: true, out var participationType))
        {
            return Results.Problem(
                "Invalid participation type. Allowed values: Waitlist, IndividualContact.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var location = (Location)new PhysicalLocation(new Address(
            request.Street,
            request.HouseNumber,
            request.ZipCode,
            request.City));

        var command = new CreateVolunteerOpportunityCommand(
            request.Title,
            request.Description,
            new OrganizationId(request.OrganizationId),
            location,
            occurrence,
            participationType);

        var opportunity = await sender.Send(command, cancellationToken);

        var address = opportunity.Location is PhysicalLocation pl ? pl.Address : null;

        var response = new CreateVolunteerOpportunityResponse(
            opportunity.Id.Value,
            opportunity.Title,
            opportunity.Description,
            opportunity.OrganizationId.Value,
            address?.Street,
            address?.HouseNumber,
            address?.ZipCode,
            address?.City,
            opportunity.Location is RemoteLocation,
            opportunity.Occurrence.ToString(),
            opportunity.ParticipationType.ToString(),
            opportunity.CreatedOn);

        return Results.Ok(response);
    }
}
