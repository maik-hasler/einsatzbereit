using Application.Messaging;
using Domain.Bedarfe;
using Domain.Organisationen;

namespace Application.Bedarfe.CreateBedarf.v1;

public sealed record CreateBedarfCommand(
    string Title,
    string Description,
    OrganisationId OrganisationId)
    : IRequest<Bedarf>;