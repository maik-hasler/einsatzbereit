using System.Security.Claims;
using Api.Abstractions;
using Api.Authentication;
using Application.Abstractions;
using Application.Messaging;
using Application.Organisationen.GetOrganisationen.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organisationen.GetOrganisationen.v1;

internal sealed class GetOrganisationenEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/organisationen", GetOrganisationenAsync)
            .WithName("GetOrganisationen")
            .Produces<IReadOnlyList<KeycloakOrganisation>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> GetOrganisationenAsync(
        ClaimsPrincipal user,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(user.FindFirstValue("sub")!);

        var query = new GetOrganisationenQuery(userId);

        var result = await sender.Send(query, cancellationToken);

        return Results.Ok(result);
    }
}
