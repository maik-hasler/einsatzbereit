using Application.Abstractions;
using Application.Messaging;

namespace Application.Organizations.GetOrganizations.v1;

public sealed record GetOrganizationsQuery(
    Guid UserId)
    : IRequest<IReadOnlyList<KeycloakOrganization>>;
