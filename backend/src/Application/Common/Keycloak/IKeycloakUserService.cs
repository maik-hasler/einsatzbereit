namespace Application.Common.Keycloak;

public record KeycloakUserProfile(
	Guid Id,
	string Username,
	string? FirstName,
	string? LastName,
	string Email);

public record AdminUserListItem(
	Guid Id,
	string Username,
	string? FirstName,
	string? LastName,
	string Email,
	bool Enabled,
	IReadOnlyList<string> RealmRoles);

public interface IKeycloakUserService
{
	Task<KeycloakUserProfile> GetUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
		IReadOnlyList<Guid> userIds,
		CancellationToken cancellationToken = default);

	Task UpdateUserAsync(
		Guid userId,
		string? firstName,
		string? lastName,
		CancellationToken cancellationToken = default);

	Task DeleteUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(
		string? search,
		int max = 100,
		CancellationToken cancellationToken = default);

	Task SetUserEnabledAsync(
		Guid userId,
		bool enabled,
		CancellationToken cancellationToken = default);

	Task AssignAdminRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default);

	Task RemoveAdminRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default);

	Task<bool> IsServiceAccountAsync(
		Guid userId,
		CancellationToken cancellationToken = default);
}
