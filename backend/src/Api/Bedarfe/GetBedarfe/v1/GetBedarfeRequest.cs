namespace Api.Bedarfe.GetBedarfe.v1;

public sealed record GetBedarfeRequest(
    int PageNumber,
    int PageSize);