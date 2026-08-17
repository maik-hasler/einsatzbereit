using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Domain.Organizations;
using Domain.Users;
using Infrastructure.Persistence;
using Infrastructure.Persistence.StartupTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

// One-shot compensator for pre-existing organizations that predate the
// organization_membership table (migration AddOrganizationMembership). Without it,
// every member of an org created before that migration has zero membership rows:
// organizers get 403'd out of their own org, since OwnershipGuard.EnsureIsOrganizerAsync
// (and EnsureIsMemberAsync for plain members) has no Keycloak fallback at request time,
// and the admin org list's member count - which reads this table directly - undercounts
// against the org's own Keycloak-sourced members page (#1895).
// Organizations created after that migration always get their founding organizer's
// membership row written synchronously by CreateOrganizationCommandHandler, so once
// this has run once against every organization that existed before the table did,
// there is nothing left for it to ever do again.
//
// Runs as a fire-and-forget background task from StartAsync (never awaited there) so
// - unlike the old inline Program.cs call it replaces - it cannot block the app from
// becoming ready. The OrganizationMembershipBackfillState marker row makes the "has this
// run before" check itself a single indexed lookup instead of a per-organization
// Keycloak round trip repeated on every boot forever (#1393): without a marker, an
// organization that legitimately still has zero organizers (not just one never
// backfilled) would look identical to "needs backfilling" and get re-queried every
// single restart.
internal sealed class OrganizationMembershipBackfillJob(
	IServiceScopeFactory scopeFactory,
	ILogger<OrganizationMembershipBackfillJob> logger)
	: IHostedService, IAsyncDisposable
{
	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_executeTask = RunAsync(_cts.Token);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_cts is not null)
			await _cts.CancelAsync();

		try
		{
			await _executeTask.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
		}
	}

	public ValueTask DisposeAsync()
	{
		_cts?.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var keycloakOrganizationService = scope.ServiceProvider.GetRequiredService<IKeycloakOrganizationService>();

		await BackfillAsync(dbContext, keycloakOrganizationService, logger, cancellationToken);
	}

	// Exposed so IntegrationTests can exercise the marker/backfill logic directly against
	// a real ApplicationDbContext, without rebooting the whole app to reproduce a
	// pre-existing-organization-without-membership-rows scenario.
	internal static async Task BackfillAsync(
		ApplicationDbContext dbContext,
		IKeycloakOrganizationService keycloakOrganizationService,
		ILogger logger,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (await dbContext.Set<OrganizationMembershipBackfillState>().AnyAsync(cancellationToken))
				return;

			var organizationIds = await dbContext.Set<Organization>()
				.Where(o => !dbContext.Set<OrganizationMembership>().Any(m => m.OrganizationId == o.Id))
				.Select(o => o.Id)
				.ToListAsync(cancellationToken);

			if (organizationIds.Count > 0)
			{
				// Realm-wide organizer set fetched once for the whole run, not per
				// organization - GetMembersAsync's own IsOrganisator flag can't be used
				// here since it now reads the very organization_membership rows this job
				// exists to create (see #1386), which are empty for every organization in
				// organizationIds by definition.
				var realmOrganizerIds = await keycloakOrganizationService.GetRealmOrganisatorUserIdsAsync(cancellationToken);

				foreach (var organizationId in organizationIds)
				{
					var members = await keycloakOrganizationService.GetMembersAsync(
						organizationId.Value, cancellationToken);

					// Every Keycloak member gets a row here, not just organizers - a plain
					// member left out would have no way to ever get one afterwards (nothing else
					// backfills), which under-counted this organization's membership everywhere
					// that reads organization_membership instead of calling Keycloak directly (#1895).
					foreach (var member in members.DistinctBy(m => m.UserId))
					{
						var role = realmOrganizerIds.Contains(member.UserId)
							? OrganizationMemberRole.Organizer
							: OrganizationMemberRole.Member;

						dbContext.Set<OrganizationMembership>().Add(
							OrganizationMembership.Create(
								organizationId, UserId.Create(member.UserId).GetValueOrThrow(), role));
					}
				}
			}

			// Written unconditionally (even when organizationIds is empty, or an organization
			// in it turned out to have zero organizers) - this pass has now covered every
			// organization that existed at startup, and that's the only thing this marker
			// promises. It deliberately is not scoped per-organization.
			dbContext.Set<OrganizationMembershipBackfillState>().Add(
				new OrganizationMembershipBackfillState { CompletedOnUtc = DateTime.UtcNow });

			await dbContext.SaveChangesAsync(cancellationToken);

			if (organizationIds.Count > 0)
				logger.LogInformation(
					"Backfilled organization_membership rows for {Count} pre-existing organization(s).",
					organizationIds.Count);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// No marker is written on failure, so a transient Keycloak outage during this
			// run simply retries on the next restart instead of being marked done falsely.
			// OperationCanceledException is let through instead of logged as an error - that's
			// just an ordinary app shutdown racing a still-running first backfill, not a failure.
			logger.LogError(
				ex,
				"An exception occurred while backfilling organization memberships");
		}
	}
}
