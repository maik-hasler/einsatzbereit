using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.SearchAlerts;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Users.SearchAlertDigest.v1;

// Consumer of SearchAlertMatchesFoundDomainEvent (#1090): SearchAlertDigestJob only
// detects which opportunities newly match a user's alert and atomically claims + queues
// them into the outbox (Infrastructure/BackgroundJobs/SearchAlertDigestJob.cs); actual
// delivery (one in-app Notification per matched opportunity, plus a single digest email)
// happens here, dispatched by OutboxProcessorJob like every other domain event, so a
// transient failure is retried on the next poll cycle instead of being lost.
internal sealed class SearchAlertMatchesFoundNotificationHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	ILogger<SearchAlertMatchesFoundNotificationHandler> logger)
	: INotificationHandler<SearchAlertMatchesFoundDomainEvent>
{
	public async Task Handle(
		SearchAlertMatchesFoundDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunityIds = notification.OpportunityIds
			.Select(id => VolunteerOpportunityId.Create(id).GetValueOrThrow())
			.ToList();

		var opportunities = await dbContext.GetVolunteerOpportunitiesByIdsAsync(opportunityIds, cancellationToken);

		if (opportunities.Count == 0)
		{
			// Every matched opportunity was deleted between the job's scan and this
			// dispatch - nothing left to notify about, and retrying would never
			// resolve, so this is treated as handled rather than re-thrown for the
			// outbox to retry forever.
			logger.LogWarning(
				"Skipping search alert digest {SearchAlertId}: none of the {Count} matched opportunities still exist",
				notification.SearchAlertId.Value,
				notification.OpportunityIds.Count);
			return;
		}

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.RecipientId], cancellationToken))[0];
		var volunteer = await keycloakUserService.GetUserAsync(notification.RecipientId.Value, cancellationToken);

		var displayName = $"{volunteer.FirstName} {volunteer.LastName}".Trim();
		if (string.IsNullOrEmpty(displayName))
			displayName = volunteer.Username;

		var language = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

		var opportunitiesList = string.Join('\n', opportunities.Select(o => $"- {o.Title}"));

		var content = emailTemplateRenderer.Render(
			opportunities.Count == 1 ? EmailTemplateKind.SearchAlertNewMatchesSingle : EmailTemplateKind.SearchAlertNewMatches,
			language,
			new Dictionary<string, string>
			{
				["DisplayName"] = displayName,
				["Count"] = opportunities.Count.ToString(),
				["OpportunitiesList"] = opportunitiesList,
			});

		// Sent (and checked) before the in-app Notification rows below are added -
		// a failed send throws here with nothing yet written to the DB, so a retry
		// re-runs this handler from scratch instead of piling up duplicate
		// Notification rows for a matched set that already got its in-app rows on
		// an earlier attempt.
		var results = await emailService.SendBatchAsync(
			[new EmailMessage(volunteer.Email, content.Subject, content.Body, notification.SearchAlertId.Value.ToString())],
			cancellationToken);

		if (!results[0])
			throw new InvalidOperationException(
				$"Failed to send search alert digest email for search alert {notification.SearchAlertId.Value}");

		foreach (var opportunity in opportunities)
			await dbContext.Notifications.AddAsync(
				Notification.Create(notification.RecipientId, NotificationKind.NewMatchingOpportunity, opportunity.Id.Value),
				cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		logger.LogInformation(
			"Sent search alert digest for {SearchAlertId}: {Count} matched opportunity/opportunities",
			notification.SearchAlertId.Value,
			opportunities.Count);
	}
}
