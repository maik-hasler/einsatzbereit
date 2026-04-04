namespace Application.Common.Keycloak;

public record KeycloakOrganization(
    Guid Id,
    string Name);

public interface IKeycloakOrganizationService
{
    Task<Guid> CreateOrganizationAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AssignOrganizerRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeycloakOrganization>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
