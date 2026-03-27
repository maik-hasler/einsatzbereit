using Application.Messaging;
using Domain.Bedarfe;

namespace Application.Bedarfe.CreateBedarf.v1;

public sealed record CreateBedarfCommand(
    string Title,
    string Description)
    : IRequest<Bedarf>;