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

	// Exact-name lookup. Lets a caller make organization creation idempotent - retrying
	// after a partial failure (e.g. ApplicationDbContextInitializer.SeedAsync's own
	// SaveChangesAsync failing after the Keycloak call already succeeded, #1212) can
	// look this up first and reuse the existing organization instead of creating a
	// second, orphaned one. Returns null rather than throwing when nothing matches.
	Task<Guid?> FindOrganizationByNameAsync(
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

	// Symmetric counterpart to AssignOrganizerRoleAsync, for demoting a member
	// away from the Organizer tier. Callers must only invoke this once the
	// user holds no remaining Organizer membership in any organization - the
	// role is realm-wide, not per-organization, so revoking it while the user
	// still organizes a different org would lock them out of that org too.
	Task RevokeOrganizerRoleAsync(
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
