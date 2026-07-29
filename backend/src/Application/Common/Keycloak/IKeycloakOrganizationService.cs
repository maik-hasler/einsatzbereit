namespace Application.Common.Keycloak;

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

	Task DeleteOrganizationAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default);

	Task AssignOrganizerRoleAsync(
		Guid userId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<KeycloakOrganizationMember>> GetMembersAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default);

	// Realm-wide, not scoped to an organization - Keycloak has no per-organization
	// organisator role to query. Reserved for one-shot reconciliation
	// (OrganizationMembershipBackfillJob) against the local organization_membership
	// table; GetMembersAsync answers IsOrganisator from that table instead, since
	// calling this on every request is exactly the perf/correctness bug fixed by #1386.
	Task<IReadOnlySet<Guid>> GetRealmOrganisatorUserIdsAsync(
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<KeycloakOrganizationMember>> SearchUsersAsync(
		string search,
		int max = 20,
		CancellationToken cancellationToken = default);
}
