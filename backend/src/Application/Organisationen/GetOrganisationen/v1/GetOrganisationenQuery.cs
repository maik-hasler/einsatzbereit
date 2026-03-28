using Application.Abstractions;
using Application.Messaging;

namespace Application.Organisationen.GetOrganisationen.v1;

public sealed record GetOrganisationenQuery(
    Guid UserId)
    : IRequest<IReadOnlyList<KeycloakOrganisation>>;
