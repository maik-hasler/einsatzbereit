namespace Infrastructure.Persistence.StartupTasks;

// Singleton marker row: its presence means OrganizationMembershipBackfillJob has
// completed once and must never run again, regardless of whether some organization
// still has zero organization_membership rows for legitimate reasons (e.g. every
// organizer left). Without this, the job's own "any org missing rows" selector would
// re-trigger a Keycloak call for that organization on every single boot, forever (#1393).
internal sealed class OrganizationMembershipBackfillState
{
	public int Id { get; init; } = 1;

	public DateTime CompletedOnUtc { get; init; }
}
