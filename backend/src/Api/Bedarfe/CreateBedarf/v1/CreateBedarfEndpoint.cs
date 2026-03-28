using Api.Abstractions;
using Api.Authentication;
using Application.Bedarfe.CreateBedarf.v1;
using Application.Messaging;
using Domain.Bedarfe;
using Domain.Organisationen;
using Microsoft.AspNetCore.Mvc;

namespace Api.Bedarfe.CreateBedarf.v1;

internal sealed class CreateBedarfEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/bedarfe", CreateBedarfAsync)
            .WithName("CreateBedarf")
            .Produces<Bedarf>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> CreateBedarfAsync(
        [FromBody] CreateBedarfRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateBedarfCommand(
            request.Title,
            request.Description,
            new OrganisationId(request.OrganisationId));

        var result = await sender.Send(command, cancellationToken);

        return Results.Ok(result);
    }
}