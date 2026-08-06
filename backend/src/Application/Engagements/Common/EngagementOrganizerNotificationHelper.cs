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

/// <summary>
/// Emails every subscribed organizer of an opportunity about a volunteer's sign-up,
/// reactivation, or withdrawal - shared by EngagementCreatedDomainEventHandler,
/// EngagementReactivatedDomainEventHandler, and EngagementWithdrawnDomainEventHandler
/// (einsatzbereit#1174). Runs off the transactional outbox rather than inline in the
/// triggering create/withdraw request, so a rapid sign-up/withdraw loop - a withdrawn
/// or cancelled engagement can be reused via Engagement.Reactivate - no longer holds
/// the request's DB transaction open across one synchronous SMTP send per organizer.
/// </summary>
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
			// Deleted between the triggering command committing and the outbox
			// dispatching this event - nothing left to notify about, and retrying
			// would never resolve.
			logger.LogWarning(
				"Skipping organizer notification for opportunity {OpportunityId}: it no longer exists",
				opportunityId.Value);
			return;
		}

		// DeleteMyAccountCommandHandler withdraws non-terminal engagements and raises
		// UserAccountDeletedDomainEvent in the same commit (#1140/#1141) - both are
		// dispatched from the same outbox batch with no ordering guarantee between
		// them, so this can legitimately run after the volunteer's Keycloak identity
		// is already deleted (most reachable via EngagementWithdrawnDomainEventHandler,
		// but the same race applies to a sign-up/reactivation followed by an
		// immediate account deletion). Retrying would never resolve that, so skip
		// the notification rather than dead-lettering forever.
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
					["OpportunityTitle"] = opportunity.Title,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				organizerId, organizerUser.UnsubscribeToken, subscriptionType);

			await emailService.SendAsync(
				organizer.Email,
				content.Subject,
				EmailFooter.Append(emailTemplateRenderer, organizerLanguage, content.Body, unsubscribeUrl),
				engagementId.Value.ToString(),
				cancellationToken);
		}
	}
}
