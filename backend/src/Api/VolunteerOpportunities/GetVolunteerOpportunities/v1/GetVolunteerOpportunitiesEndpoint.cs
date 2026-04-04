using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.GetVolunteerOpportunities.v1;

internal sealed class GetVolunteerOpportunitiesEndpoint
    : IEndpoint
{
    public void MapEndpoint(
        IEndpointRouteBuilder app)
    {
        app.MapGet("/volunteer-opportunities", GetVolunteerOpportunitiesAsync)
            .WithName("GetVolunteerOpportunities")
            .Produces<PagedList<VolunteerOpportunitySummary>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AllowAnonymous()
            .MapToApiVersion(1);
    }

    private static async Task<IResult> GetVolunteerOpportunitiesAsync(
        [AsParameters] GetVolunteerOpportunitiesRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetVolunteerOpportunitiesQuery(request.PageNumber, request.PageSize);

        var result = await sender.Send(query, cancellationToken);

        return Results.Ok(result);
    }
}
