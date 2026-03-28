using System.Security.Claims;
using Api.Abstractions;
using Api.Authentication;
using Application.Messaging;
using Application.Organisationen.CreateOrganisation.v1;
using Domain.Organisationen;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organisationen.CreateOrganisation.v1;

internal sealed class CreateOrganisationEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/organisationen", CreateOrganisationAsync)
            .WithName("CreateOrganisation")
            .Produces<Organisation>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> CreateOrganisationAsync(
        [FromBody] CreateOrganisationRequest request,
        ClaimsPrincipal user,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateOrganisationCommand(request.Name, userId);

        var result = await sender.Send(command, cancellationToken);

        return Results.Ok(result);
    }
}
