using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CancelEngagement.v1;

// Consumer of EngagementCancelledDomainEvent (#1150): every path that cancels an
// engagement (organizer-triggered CancelEngagementCommandHandler, and the
// opportunity delete/cancel/unpublish cascades via EngagementCancellationHelper)
// only calls Engagement.Cancel() and raises the event; the volunteer's cancellation
// email happens here, dispatched by OutboxProcessorJob like every other domain
// event, so a transient email failure is retried on the next poll cycle instead of
// having already been sent before the triggering command's transaction could even commit.
internal sealed class EngagementCancelledNotificationHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
	ILogger<EngagementCancelledNotificationHandler> logger)
	: INotificationHandler<EngagementCancelledDomainEvent>
{
	public async Task Handle(
		EngagementCancelledDomainEvent notification,
		CancellationToken cancellationToken)
	{
		// Prefer the title carried on the event itself: several callers cancel an
		// engagement as part of deleting/shadow-deleting its opportunity in the same
		// transaction, so by the time this dispatches post-commit there is nothing
		// left to look up (the opportunity row is gone, or filtered out as deleted).
		// Falling back to a live lookup only covers callers that didn't have a title
		// handy to pass into Cancel().
		var opportunityTitle = notification.OpportunityTitle
			?? (await dbContext.VolunteerOpportunities.FindAsync(notification.OpportunityId, cancellationToken))?.Title;

		if (opportunityTitle is null)
		{
			logger.LogWarning(
				"Skipping cancellation email for engagement {EngagementId}: opportunity title unavailable for {OpportunityId}",
				notification.EngagementId.Value,
				notification.OpportunityId.Value);
			return;
		}

		var volunteerUser = (await dbContext.GetOrCreateUsersAsync([notification.VolunteerId], cancellationToken))[0];
		if (!volunteerUser.IsSubscribedTo(EmailNotificationType.EngagementCancelled))
			return;

		var volunteer = await keycloakUserService.GetUserAsync(notification.VolunteerId.Value, cancellationToken);
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

		var reasonBlock = string.IsNullOrWhiteSpace(notification.Reason)
			? string.Empty
			: emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementCancelledReasonSuffix,
				volunteerLanguage,
				new Dictionary<string, string> { ["Reason"] = notification.Reason }).Body;

		var content = emailTemplateRenderer.Render(
			EmailTemplateKind.EngagementCancelled,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
				["OpportunityTitle"] = opportunityTitle,
				["ReasonBlock"] = reasonBlock,
			});

		var unsubscribeUrl = unsubscribeLinkBuilder.Build(
			notification.VolunteerId, volunteerUser.UnsubscribeToken, EmailNotificationType.EngagementCancelled);

		await emailService.SendAsync(
			volunteer.Email,
			content.Subject,
			EmailFooter.Append(content.Body, unsubscribeUrl),
			cancellationToken);
	}
}
