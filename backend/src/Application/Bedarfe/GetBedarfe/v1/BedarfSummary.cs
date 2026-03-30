using Domain.Bedarfe;

namespace Application.Bedarfe.GetBedarfe.v1;

public sealed record BedarfSummary(
    Guid Id,
    string Title,
    string Description,
    string OrganisationName,
    Adresse Adresse,
    Frequenz Frequenz,
    DateTimeOffset CreatedOn);
