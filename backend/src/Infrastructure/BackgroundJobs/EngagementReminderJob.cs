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
			.ToListAsync(ct);

		foreach (var item in engagements)
		{
			try
			{
				var user = await keycloakUserService.GetUserAsync(item.Engagement.VolunteerId!.Value.Value, ct);

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

				await emailService.SendAsync(user.Email, subject, body, ct);

				item.Engagement.MarkReminderSent(now);
				await dbContext.SaveChangesAsync(ct);

				logger.LogInformation(
					"Sent 24h reminder to {Email} for engagement {EngagementId}",
					user.Email,
					item.Engagement.Id.Value);
			}
			catch (Exception ex)
			{
				logger.LogError(
					ex,
					"Failed to send reminder for engagement {EngagementId}",
					item.Engagement.Id.Value);
			}
		}
	}
}
