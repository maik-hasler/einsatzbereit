using Application.Common.Email;
using Application.Common.Keycloak;
using Domain.Engagements;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

internal sealed class EngagementReminderJob(
	IServiceScopeFactory scopeFactory,
	ILogger<EngagementReminderJob> logger)
	: IHostedService, IAsyncDisposable
{
	// Caps how many engagements one hourly tick processes. Anything left over
	// still has ReminderSentAt == null and its TimeSlot still falls in the
	// (now+23h, now+25h) window on the next tick (the window is 2h wide, the
	// timer fires every 1h), so it is picked up then instead of being lost.
	private const int MaxBatchSize = 500;

	private Task _executeTask = Task.CompletedTask;
	private CancellationTokenSource? _cts;
	private PeriodicTimer? _timer;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_timer = new PeriodicTimer(TimeSpan.FromHours(1));
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
			await SendRemindersAsync(ct).ConfigureAwait(false);
		}
	}

	private async Task SendRemindersAsync(CancellationToken ct)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
		var keycloakUserService = scope.ServiceProvider.GetRequiredService<IKeycloakUserService>();

		var now = DateTimeOffset.UtcNow;
		var windowStart = now.AddHours(23);
		var windowEnd = now.AddHours(25);

		var engagements = await dbContext.Set<Engagement>()
			.Where(e =>
				e.Status == EngagementStatus.Confirmed &&
				e.TimeSlotId != null &&
				e.ReminderSentAt == null)
			.Join(
				dbContext.Set<TimeSlot>(),
				e => e.TimeSlotId,
				ts => ts.Id,
				(e, ts) => new { Engagement = e, TimeSlot = ts })
			.Where(x => x.TimeSlot.StartDateTime >= windowStart && x.TimeSlot.StartDateTime <= windowEnd)
			.Join(
				dbContext.Set<VolunteerOpportunity>(),
				x => x.Engagement.OpportunityId,
				vo => vo.Id,
				(x, vo) => new { x.Engagement, x.TimeSlot, OpportunityTitle = vo.Title })
			.OrderBy(x => x.TimeSlot.StartDateTime)
			.Take(MaxBatchSize)
			.ToListAsync(ct);

		if (engagements.Count == 0)
			return;

		// One email per engagement, resolved sequentially: KeycloakUserService
		// mutates a shared HttpClient auth header per call (see its
		// SendAuthorizedAsync comment), so these lookups cannot run concurrently.
		// Caching by volunteer avoids repeating the lookup for a volunteer
		// confirmed for more than one slot inside this run's window.
		var profileCache = new Dictionary<Guid, KeycloakUserProfile>();
		var messages = new List<EmailMessage>(engagements.Count);
		var recipients = new List<(Engagement Engagement, string Email)>(engagements.Count);

		foreach (var item in engagements)
		{
			var volunteerId = item.Engagement.VolunteerId!.Value.Value;
			try
			{
				if (!profileCache.TryGetValue(volunteerId, out var user))
				{
					user = await keycloakUserService.GetUserAsync(volunteerId, ct);
					profileCache[volunteerId] = user;
				}

				var displayName = $"{user.FirstName} {user.LastName}".Trim();
				if (string.IsNullOrEmpty(displayName))
					displayName = user.Username;

				var startFormatted = item.TimeSlot.StartDateTime.ToLocalTime().ToString("dddd, d. MMMM yyyy 'at' HH:mm");

				var subject = $"Reminder: {item.OpportunityTitle} starts tomorrow";
				var body =
					$"Hi {displayName},\n\n" +
					$"This is a reminder that you are signed up for \"{item.OpportunityTitle}\" " +
					$"which starts on {startFormatted}.\n\n" +
					$"We are looking forward to seeing you!\n\n" +
					$"The Einsatzbereit Team";

				messages.Add(new EmailMessage(user.Email, subject, body));
				recipients.Add((item.Engagement, user.Email));
			}
			catch (Exception ex)
			{
				logger.LogError(
					ex,
					"Failed to resolve volunteer profile for engagement {EngagementId}",
					item.Engagement.Id.Value);
			}
		}

		if (messages.Count == 0)
			return;

		// A single SMTP connection for the whole batch instead of one per engagement.
		var sendResults = await emailService.SendBatchAsync(messages, ct);

		var sentIds = new List<EngagementId>(recipients.Count);
		for (var i = 0; i < recipients.Count; i++)
		{
			if (!sendResults[i])
				continue;

			var (engagement, email) = recipients[i];
			sentIds.Add(engagement.Id);

			logger.LogInformation(
				"Sent 24h reminder to {Email} for engagement {EngagementId}",
				email,
				engagement.Id.Value);
		}

		if (sentIds.Count == 0)
			return;

		try
		{
			// A single batched UPDATE instead of one SaveChangesAsync per engagement.
			await dbContext.Set<Engagement>()
				.Where(e => sentIds.Contains(e.Id))
				.ExecuteUpdateAsync(
					s => s
						.SetProperty(e => e.ReminderSentAt, now)
						.SetProperty(e => e.ModifiedOn, now),
					ct);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to persist ReminderSentAt for {Count} engagements after sending reminders; they will be retried next run",
				sentIds.Count);
		}
	}
}
