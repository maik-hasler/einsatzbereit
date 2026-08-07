using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.Common;

/// <summary>
/// Emails the volunteer their own sign-up/reactivation receipt - shared by
/// EngagementCreatedDomainEventHandler and EngagementReactivatedDomainEventHandler
/// (einsatzbereit#1729). Runs off the transactional outbox rather than inline in
/// the triggering create/reactivate request, so the time-slot row lock (#1142)
/// held by CreateEngagementCommandHandler no longer stays open across a
/// synchronous SMTP send. Unlike the organizer notification
/// (EngagementOrganizerNotificationHelper), this is never gated by preference
/// (#1055): it's the direct, synchronous-feeling response to the volunteer's own
/// just-submitted action, not a repeatable notification about someone else's
/// activity.
/// </summary>
internal static class EngagementVolunteerConfirmationHelper
{
	public static async Task NotifyAsync(
		IApplicationDbContext dbContext,
		IKeycloakUserService keycloakUserService,
		IEmailService emailService,
		IEmailTemplateRenderer emailTemplateRenderer,
		EngagementId engagementId,
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		bool isSlotSignUp,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(opportunityId, cancellationToken);
		if (opportunity is null)
		{
			// Deleted between the triggering command committing and the outbox
			// dispatching this event - nothing left to confirm, and retrying would
			// never resolve.
			logger.LogWarning(
				"Skipping volunteer confirmation for opportunity {OpportunityId}: it no longer exists",
				opportunityId.Value);
			return;
		}

		KeycloakUserProfile volunteer;
		try
		{
			volunteer = await keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken);
		}
		catch (Exception ex)
		{
			// Same race as EngagementOrganizerNotificationHelper: an immediate
			// account deletion can beat this event out of the outbox. Retrying
			// would never resolve that, so skip rather than dead-letter forever.
			logger.LogWarning(
				ex,
				"Skipping volunteer confirmation for engagement {EngagementId}: volunteer {VolunteerId} could not be looked up in Keycloak",
				engagementId.Value,
				volunteerId.Value);
			return;
		}

		var volunteerName = volunteer.FirstName ?? volunteer.Username;
		var volunteerUser = await dbContext.Users.FindAsync(volunteerId, cancellationToken);
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser?.PreferredLanguage);

		var content = emailTemplateRenderer.Render(
			isSlotSignUp ? EmailTemplateKind.EngagementWaitlisted : EmailTemplateKind.EngagementRequestReceived,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteerName,
				["OpportunityTitle"] = opportunity.Title,
			});

		await emailService.SendAsync(
			volunteer.Email, content.Subject, content.Body, engagementId.Value.ToString(), cancellationToken);
	}
}
