namespace Api.Bedarfe.CreateBedarf.v1;

public sealed record CreateBedarfResponse(
    Guid Id,
    string Title,
    string Description,
    Guid OrganisationId,
    string Strasse,
    string Hausnummer,
    string Plz,
    string Ort,
    string Frequenz,
    string Status,
    DateTimeOffset CreatedOn);
