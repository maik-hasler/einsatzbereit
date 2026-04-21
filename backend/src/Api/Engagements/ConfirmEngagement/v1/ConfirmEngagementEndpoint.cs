using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Engagements.ConfirmEngagement.v1;
using Domain.Engagements;
using Domain.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.ConfirmEngagement.v1;

internal sealed class ConfirmEngagementEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/engagements/{engagementId:guid}/confirm", ConfirmEngagementAsync)
            .WithName("ConfirmEngagement")
            .WithTags("Engagements")
            .Produces<EngagementStatusResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> ConfirmEngagementAsync(
        [FromRoute] Guid engagementId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ConfirmEngagementCommand(new EngagementId(engagementId));
            var engagement = await sender.Send(command, cancellationToken);
            return Results.Ok(new EngagementStatusResponse(engagement.Id.Value, engagement.Status.ToString(), engagement.ModifiedOn));
        }
        catch (DomainException ex) when (ex.Message.Contains("not found"))
        {
            return Results.NotFound();
        }
        catch (DomainException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
