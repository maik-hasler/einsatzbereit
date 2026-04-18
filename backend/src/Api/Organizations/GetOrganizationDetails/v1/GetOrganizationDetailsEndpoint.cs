using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Organizations.GetOrganizationDetails.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.GetOrganizationDetails.v1;

internal sealed class GetOrganizationDetailsEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/organizations/{organizationId:guid}", GetOrganizationDetailsAsync)
            .WithName("GetOrganizationDetails")
            .Produces<OrganizationDetailsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> GetOrganizationDetailsAsync(
        [FromRoute] Guid organizationId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetOrganizationDetailsQuery(organizationId);

        var result = await sender.Send(query, cancellationToken);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
