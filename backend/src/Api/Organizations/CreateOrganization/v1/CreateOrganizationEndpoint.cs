using System.Security.Claims;
using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Organizations.CreateOrganization.v1;
using Domain.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.CreateOrganization.v1;

internal sealed class CreateOrganizationEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/organizations", CreateOrganizationAsync)
            .WithName("CreateOrganization")
            .Produces<Organization>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        [FromBody] CreateOrganizationRequest request,
        ClaimsPrincipal user,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateOrganizationCommand(request.Name, userId);

        var result = await sender.Send(command, cancellationToken);

        return Results.Ok(result);
    }
}
