namespace Api.Bedarfe.CreateBedarf.v1;

public sealed record CreateBedarfRequest(
    string Title,
    string Description);