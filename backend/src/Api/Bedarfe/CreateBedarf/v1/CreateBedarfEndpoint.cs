using Api.Abstractions;
using Api.Authentication;
using Application.Bedarfe.CreateBedarf.v1;
using Application.Messaging;
using Domain.Bedarfe;
using Domain.Organisationen;
using Microsoft.AspNetCore.Mvc;

namespace Api.Bedarfe.CreateBedarf.v1;

internal sealed class CreateBedarfEndpoint
    : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/bedarfe", CreateBedarfAsync)
            .WithName("CreateBedarf")
            .Produces<CreateBedarfResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationPolicies.EinsatzbereitOrganisatorPolicy)
            .MapToApiVersion(1);
    }

    private static async Task<IResult> CreateBedarfAsync(
        [FromBody] CreateBedarfRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Frequenz>(request.Frequenz, ignoreCase: true, out var frequenz))
        {
            return Results.Problem(
                "Ungültige Frequenz. Erlaubte Werte: Einmalig, Regelmaessig.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var adresse = new Adresse(
            request.Strasse,
            request.Hausnummer,
            request.Plz,
            request.Ort);

        var command = new CreateBedarfCommand(
            request.Title,
            request.Description,
            new OrganisationId(request.OrganisationId),
            adresse,
            frequenz);

        var bedarf = await sender.Send(command, cancellationToken);

        var response = new CreateBedarfResponse(
            bedarf.Id.Value,
            bedarf.Title,
            bedarf.Description,
            bedarf.OrganisationId.Value,
            bedarf.Adresse.Strasse,
            bedarf.Adresse.Hausnummer,
            bedarf.Adresse.Plz,
            bedarf.Adresse.Ort,
            bedarf.Frequenz.ToString(),
            bedarf.PublishedOn.HasValue ? "Veröffentlicht" : "Entwurf",
            bedarf.CreatedOn);

        return Results.Ok(response);
    }
}
