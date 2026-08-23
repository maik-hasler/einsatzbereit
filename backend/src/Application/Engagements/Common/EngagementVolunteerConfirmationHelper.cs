using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.Common;

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
				["OpportunityTitle"] = opportunity.TitleDe,
			});

		await emailService.SendAsync(
			volunteer.Email, content.Subject, content.Body, engagementId.Value.ToString(), cancellationToken);
	}
}
