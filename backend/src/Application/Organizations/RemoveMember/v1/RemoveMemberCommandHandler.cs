using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Organizations.RemoveMember.v1;

internal sealed class RemoveMemberCommandHandler(
    IKeycloakOrganizationService keycloakOrganizationService)
    : ICommandHandler<RemoveMemberCommand, bool>
{
    public async ValueTask<bool> Handle(
        RemoveMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        await keycloakOrganizationService.RemoveMemberAsync(
            request.OrganizationId, request.UserId, cancellationToken);

        return true;
    }
}
