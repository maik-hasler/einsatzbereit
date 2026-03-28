namespace Domain.Organisationen;

public interface IOrganisationRepository
{
    ValueTask AddAsync(
        Organisation organisation,
        CancellationToken cancellationToken = default);

    ValueTask<Organisation?> GetByKeycloakIdAsync(
        Guid keycloakId,
        CancellationToken cancellationToken = default);
}
