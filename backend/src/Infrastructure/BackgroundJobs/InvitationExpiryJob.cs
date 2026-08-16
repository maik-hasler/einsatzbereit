using Application.Common.Exceptions;
using Domain.Organizations;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.BackgroundJobs;

// Flips Pending organization invitations whose 14-day window has elapsed to
// Expired (#1053), so they stop cluttering the invitee's "open invitations"
// list and become eligible for an organizer's Resend action. No outbox/domain
// event involved: unlike EngagementReminderJob this has no side effect to
// dedupe across replicas (expiring twice is a harmless no-op, not a duplicate
// email), so a plain read -> Expire(now) -> SaveChanges per tick is enough.
internal sealed class InvitationExpiryJob(
	IServiceScopeFactory scopeFactory,
	ILogger<InvitationExpiryJob> logger,
	IOptions<InvitationExpiryOptions> options)
	: IHostedService, IAsyncDisposable
{
	private readonly InvitationExpiryOptions _options = options.Value;

	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(TimeSpan.FromHours(_options.PollIntervalHours));
		_executeTask = RunLoopAsync(_cts.Token);
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
		_timer?.Dispose();
		_cts?.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task RunLoopAsync(CancellationToken ct)
	{
		if (_timer is null) return;

		while (!ct.IsCancellationRequested && await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
		{
			try
			{
				await TickAsync(ct).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// A due invitation is only marked Expired once this succeeds, so a
				// transient failure here (e.g. a DB blip) just means it is picked up
				// again on the next tick instead of being lost.
				logger.LogError(ex, "Invitation expiry tick failed; will retry on the next poll interval");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var expired = await ExpireDueInvitationsAsync(dbContext, DateTimeOffset.UtcNow, ct);

		if (expired > 0)
			logger.LogInformation("Expired {Count} invitation(s)", expired);
	}

	// Exposed so IntegrationTests can exercise expiry directly against a real
	// ApplicationDbContext instead of waiting for a real tick.
	internal static async Task<int> ExpireDueInvitationsAsync(
		ApplicationDbContext dbContext,
		DateTimeOffset now,
		CancellationToken cancellationToken = default)
	{
		var dueInvitations = await dbContext.Set<OrganizationInvitation>()
			.Where(i => i.Status == InvitationStatus.Pending && i.ExpiresOn <= now)
			.ToListAsync(cancellationToken);

		if (dueInvitations.Count == 0)
			return 0;

		foreach (var invitation in dueInvitations)
		{
			invitation.Expire(now).ThrowIfFailure();

			// #1919: an expired invitation is just as resolved as an
			// accepted/declined one - without this, its InvitationReceived
			// notification stuck around indefinitely (it's never marked read
			// automatically), pointing the invitee at a /my-signups page with
			// nothing left to show for it.
			await dbContext.DeleteInvitationReceivedNotificationsAsync(invitation.Id.Value, cancellationToken);
		}

		await dbContext.SaveChangesAsync(cancellationToken);

		return dueInvitations.Count;
	}
}
