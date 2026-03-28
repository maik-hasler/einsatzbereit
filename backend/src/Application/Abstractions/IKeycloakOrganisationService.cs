namespace Application.Abstractions;

public record KeycloakOrganisation(
    Guid Id,
    string Name);

public interface IKeycloakOrganisationService
{
    Task<Guid> CreateOrganisationAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        Guid organisationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AssignOrganisatorRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeycloakOrganisation>> GetUserOrganisationenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
