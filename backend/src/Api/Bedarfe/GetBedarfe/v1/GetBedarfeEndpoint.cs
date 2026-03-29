using Api.Abstractions;
using Application.Bedarfe.GetBedarfe.v1;
using Application.Messaging;
using Application.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace Api.Bedarfe.GetBedarfe.v1;

internal sealed class GetBedarfeEndpoint
    : IEndpoint
{
    public void MapEndpoint(
        IEndpointRouteBuilder app)
    {
        app.MapGet("/bedarfe", GetBedarfeAsync)
            .WithName("GetBedarfe")
            .Produces<PagedList<BedarfSummary>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AllowAnonymous()
            .MapToApiVersion(1);
    }

    private static async Task<IResult> GetBedarfeAsync(
        [AsParameters] GetBedarfeRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetBedarfeQuery(request.PageNumber, request.PageSize);

        var result = await sender.Send(query, cancellationToken);

        return Results.Ok(result);
    }
}
