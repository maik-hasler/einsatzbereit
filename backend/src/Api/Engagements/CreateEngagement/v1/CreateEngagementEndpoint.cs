using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Engagements.CreateEngagement.v1;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Engagements.CreateEngagement.v1;

internal sealed class CreateEngagementEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/volunteer-opportunities/{opportunityId:guid}/engagements", CreateEngagementAsync)
            .WithName("CreateEngagement")
            .WithTags("Engagements")
            .Produces<CreateEngagementResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitDefaultUserPolicy)
            .MapToApiVersion(1);

    private static async Task<IResult> CreateEngagementAsync(
        [FromRoute] Guid opportunityId,
        [FromBody] CreateEngagementRequest request,
        [FromServices] ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var subClaim = httpContext.User.FindFirst("sub")?.Value;
        if (subClaim is null || !Guid.TryParse(subClaim, out var volunteerId))
        {
            return Results.Problem("Unable to identify the current user.", statusCode: StatusCodes.Status401Unauthorized);
        }

        TimeSlotId? timeSlotId = request.TimeSlotId.HasValue
            ? new TimeSlotId(request.TimeSlotId.Value)
            : null;

        var command = new CreateEngagementCommand(
            new VolunteerOpportunityId(opportunityId),
            new UserId(volunteerId),
            timeSlotId,
            request.Message);

        var engagement = await sender.Send(command, cancellationToken);

        var response = new CreateEngagementResponse(
            engagement.Id.Value,
            engagement.OpportunityId.Value,
            engagement.Status.ToString(),
            engagement.CreatedOn);

        return Results.Created($"/v1/engagements/{engagement.Id.Value}", response);
    }
}
