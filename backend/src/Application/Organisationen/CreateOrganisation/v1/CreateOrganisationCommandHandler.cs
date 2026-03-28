using Application.Abstractions;
using Application.Messaging;
using Domain.Organisationen;

namespace Application.Organisationen.CreateOrganisation.v1;

internal sealed class CreateOrganisationCommandHandler(
    IKeycloakOrganisationService keycloakOrganisationService,
    IOrganisationRepository organisationRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrganisationCommand, Organisation>
{
    public async ValueTask<Organisation> Handle(
        CreateOrganisationCommand request,
        CancellationToken cancellationToken = default)
    {
        var keycloakId = await keycloakOrganisationService.CreateOrganisationAsync(
            request.Name, cancellationToken);

        await keycloakOrganisationService.AddMemberAsync(
            keycloakId, request.UserId, cancellationToken);

        await keycloakOrganisationService.AssignOrganisatorRoleAsync(
            request.UserId, cancellationToken);

        var organisation = Organisation.Create(request.Name, keycloakId);

        await organisationRepository.AddAsync(organisation, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return organisation;
    }
}
