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
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

		while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
		{
			await SendRemindersAsync(stoppingToken);
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
				var user = await keycloakUserService.GetUserAsync(item.Engagement.VolunteerId.Value, ct);

				var displayName = string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim())
					? user.Username
					: $"{user.FirstName} {user.LastName}".Trim();

				var startFormatted = item.TimeSlot.StartDateTime.ToLocalTime().ToString("dddd, d. MMMM yyyy 'at' HH:mm");

				var subject = $"Reminder: {item.OpportunityTitle} starts tomorrow";
				var body = $"Hi {displayName},\n\n" +
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
