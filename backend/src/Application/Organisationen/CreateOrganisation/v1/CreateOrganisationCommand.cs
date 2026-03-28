using Application.Messaging;
using Domain.Organisationen;

namespace Application.Organisationen.CreateOrganisation.v1;

public sealed record CreateOrganisationCommand(
    string Name,
    Guid UserId)
    : IRequest<Organisation>;
