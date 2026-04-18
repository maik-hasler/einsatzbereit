using Api.Common.Authentication;
using Api.Common.Endpoints;
using Application.Common.Messaging;
using Application.Organizations.UpdateOrganization.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.Organizations.UpdateOrganization.v1;

internal sealed class UpdateOrganizationEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/organizations/{organizationId:guid}", UpdateOrganizationAsync)
            .WithName("UpdateOrganization")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        [FromRoute] Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var addressCommand = request.Address is null
            ? null
            : new UpdateAddressCommand(
                request.Address.Street,
                request.Address.HouseNumber,
                request.Address.ZipCode,
                request.Address.City);

        var command = new UpdateOrganizationCommand(
            organizationId,
            request.Name,
            request.Description,
            request.ContactEmail,
            request.ContactPhone,
            request.Website,
            addressCommand);

        await sender.Send(command, cancellationToken);

        return Results.NoContent();
    }
}
