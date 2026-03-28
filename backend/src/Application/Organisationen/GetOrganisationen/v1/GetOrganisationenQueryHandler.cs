using Application.Abstractions;
using Application.Messaging;

namespace Application.Organisationen.GetOrganisationen.v1;

internal sealed class GetOrganisationenQueryHandler(
    IKeycloakOrganisationService keycloakOrganisationService)
    : IRequestHandler<GetOrganisationenQuery, IReadOnlyList<KeycloakOrganisation>>
{
    public async ValueTask<IReadOnlyList<KeycloakOrganisation>> Handle(
        GetOrganisationenQuery request,
        CancellationToken cancellationToken = default)
    {
        return await keycloakOrganisationService.GetUserOrganisationenAsync(
            request.UserId, cancellationToken);
    }
}
