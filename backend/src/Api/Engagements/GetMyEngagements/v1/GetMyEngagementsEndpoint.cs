using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Engagements;
using Application.Engagements.GetMyEngagements.v1;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.GetMyEngagements.v1;

internal sealed class GetMyEngagementsEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/engagements", GetMyEngagementsAsync)
            .WithName("GetMyEngagements")
            .WithTags("Engagements")
            .Produces<List<EngagementSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> GetMyEngagementsAsync(
        [FromServices] ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var subClaim = httpContext.User.FindFirst("sub")?.Value;
        if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
        {
            return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var query = new GetMyEngagementsQuery(new UserId(userId));
        var result = await sender.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
