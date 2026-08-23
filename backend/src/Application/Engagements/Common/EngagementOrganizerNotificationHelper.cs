using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.Common;

internal static class EngagementOrganizerNotificationHelper
{
	public static async Task NotifyAsync(
		IApplicationDbContext dbContext,
		IKeycloakOrganizationService keycloakOrganizationService,
		IKeycloakUserService keycloakUserService,
		IEmailService emailService,
		IEmailTemplateRenderer emailTemplateRenderer,
		IUnsubscribeLinkBuilder unsubscribeLinkBuilder,
		EngagementId engagementId,
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		EmailTemplateKind templateKind,
		EmailNotificationType subscriptionType,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(opportunityId, cancellationToken);
		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping organizer notification for opportunity {OpportunityId}: it no longer exists",
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
				"Skipping organizer notification for engagement {EngagementId}: volunteer {VolunteerId} could not be looked up in Keycloak",
				engagementId.Value,
				volunteerId.Value);
			return;
		}

		var volunteerName = volunteer.FirstName ?? volunteer.Username;

		var members = await keycloakOrganizationService
			.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);
		var organizerIds = members
			.Where(m => m.IsOrganisator)
			.Select(m => UserId.Create(m.UserId).GetValueOrThrow())
			.ToList();

		if (organizerIds.Count == 0)
			return;

		var organizerUsersById = (await dbContext.GetOrCreateUsersAsync(organizerIds, cancellationToken))
			.ToDictionary(u => u.Id);

		var messages = new List<EmailMessage>(members.Count);

		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var organizerId = UserId.Create(organizer.UserId).GetValueOrThrow();
			var organizerUser = organizerUsersById[organizerId];

			if (!organizerUser.IsSubscribedTo(subscriptionType))
				continue;

			var organizerName = organizer.FirstName ?? organizer.Username;
			var organizerLanguage = SupportedLanguages.Resolve(organizerUser.PreferredLanguage);

			var content = emailTemplateRenderer.Render(
				templateKind,
				organizerLanguage,
				new Dictionary<string, string>
				{
					["OrganizerName"] = organizerName,
					["VolunteerName"] = volunteerName,
					["OpportunityTitle"] = opportunity.TitleDe,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				organizerId, organizerUser.UnsubscribeToken, subscriptionType);

			messages.Add(new EmailMessage(
				organizer.Email,
				content.Subject,
				EmailFooter.Append(emailTemplateRenderer, organizerLanguage, content.Body, unsubscribeUrl),
				engagementId.Value.ToString()));
		}

		if (messages.Count > 0)
			await emailService.SendBatchAsync(messages, cancellationToken);
	}
}
