namespace Application.Common.Keycloak;

public record KeycloakOrganization(
    Guid Id,
    string Name);

public record KeycloakOrganizationMember(
    Guid UserId,
    string Username,
    string? FirstName,
    string? LastName,
    string Email,
    bool IsOrganisator);

public interface IKeycloakOrganizationService
{
    Task<Guid> CreateOrganizationAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AssignOrganizerRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeycloakOrganization>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeycloakOrganizationMember>> GetMembersAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
