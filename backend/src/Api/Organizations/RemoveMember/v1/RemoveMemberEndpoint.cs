using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Organizations.RemoveMember.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.RemoveMember.v1;

internal sealed class RemoveMemberEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/organizations/{organizationId:guid}/members/{userId:guid}", RemoveMemberAsync)
            .WithName("RemoveMember")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> RemoveMemberAsync(
        [FromRoute] Guid organizationId,
        [FromRoute] Guid userId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RemoveMemberCommand(organizationId, userId);

        await sender.Send(command, cancellationToken);

        return Results.NoContent();
    }
}
