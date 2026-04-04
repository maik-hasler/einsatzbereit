using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Organizations.GetOrganizations.v1;

internal sealed class GetOrganizationsQueryHandler(
    IKeycloakOrganizationService keycloakOrganizationService)
    : IQueryHandler<GetOrganizationsQuery, IReadOnlyList<KeycloakOrganization>>
{
    public async ValueTask<IReadOnlyList<KeycloakOrganization>> Handle(
        GetOrganizationsQuery request,
        CancellationToken cancellationToken = default)
    {
        return await keycloakOrganizationService.GetUserOrganizationsAsync(
            request.UserId, cancellationToken);
    }
}
