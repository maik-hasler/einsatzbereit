using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Organizations.GetOrganizations.v1;

public sealed record GetOrganizationsQuery(
    Guid UserId)
    : IQuery<IReadOnlyList<KeycloakOrganization>>;
