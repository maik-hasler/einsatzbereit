namespace Application.Common.Keycloak;

public record KeycloakUserProfile(
	Guid Id,
	string Username,
	string? FirstName,
	string? LastName,
	string Email);

public interface IKeycloakUserService
{
	Task<KeycloakUserProfile> GetUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default);

	Task UpdateUserAsync(
		Guid userId,
		string? firstName,
		string? lastName,
		CancellationToken cancellationToken = default);

	Task DeleteUserAsync(
		Guid userId,
		CancellationToken cancellationToken = default);
}
