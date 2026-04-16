using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Organizations.AddMember.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.AddMember.v1;

internal sealed class AddMemberEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/organizations/{organizationId:guid}/members", AddMemberAsync)
            .WithName("AddMember")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> AddMemberAsync(
        [FromRoute] Guid organizationId,
        [FromBody] AddMemberRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new AddMemberCommand(organizationId, request.UserId);

        await sender.Send(command, cancellationToken);

        return Results.NoContent();
    }
}
