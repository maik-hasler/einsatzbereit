using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;

namespace Application.Organizations.CreateOrganization.v1;

internal sealed class CreateOrganizationCommandHandler(
    IKeycloakOrganizationService keycloakOrganizationService,
    IApplicationDbContext dbContext)
    : ICommandHandler<CreateOrganizationCommand, Organization>
{
    public async ValueTask<Organization> Handle(
        CreateOrganizationCommand request,
        CancellationToken cancellationToken = default)
    {
        var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
            request.Name, cancellationToken);

        await keycloakOrganizationService.AddMemberAsync(
            keycloakId, request.UserId, cancellationToken);

        await keycloakOrganizationService.AssignOrganizerRoleAsync(
            request.UserId, cancellationToken);

        var organization = Organization.Create(new OrganizationId(keycloakId), request.Name);

        await dbContext.Organizations.AddAsync(organization, cancellationToken);

        return organization;
    }
}
