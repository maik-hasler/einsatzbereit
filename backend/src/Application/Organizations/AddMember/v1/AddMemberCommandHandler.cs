using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Organizations.AddMember.v1;

internal sealed class AddMemberCommandHandler(
    IKeycloakOrganizationService keycloakOrganizationService)
    : ICommandHandler<AddMemberCommand, bool>
{
    public async ValueTask<bool> Handle(
        AddMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        await keycloakOrganizationService.AddMemberAsync(
            request.OrganizationId, request.UserId, cancellationToken);

        return true;
    }
}
