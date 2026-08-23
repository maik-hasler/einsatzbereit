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
				var realmOrganizerIds = await keycloakOrganizationService.GetRealmOrganisatorUserIdsAsync(cancellationToken);

				foreach (var organizationId in organizationIds)
				{
					var members = await keycloakOrganizationService.GetMembersAsync(
						organizationId.Value, cancellationToken);

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
			logger.LogError(
				ex,
				"An exception occurred while backfilling organization memberships");
		}
	}
}
