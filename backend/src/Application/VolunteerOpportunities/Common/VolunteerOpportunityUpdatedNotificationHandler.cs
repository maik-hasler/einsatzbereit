using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.Common;

internal sealed class VolunteerOpportunityUpdatedNotificationHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	ILogger<VolunteerOpportunityUpdatedNotificationHandler> logger)
	: INotificationHandler<VolunteerOpportunityUpdatedDomainEvent>
{
	public async Task Handle(
		VolunteerOpportunityUpdatedDomainEvent notification,
		CancellationToken cancellationToken)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			notification.OpportunityId, cancellationToken);
		if (opportunity is null)
		{
			logger.LogWarning(
				"Skipping opportunity-updated email for opportunity {OpportunityId}: it no longer exists",
				notification.OpportunityId.Value);
			return;
		}

		var volunteerIds = await engagementReadRepository.GetActiveVolunteerIdsByOpportunityAsync(
			notification.OpportunityId, notification.TimeSlotId, cancellationToken);

		if (volunteerIds.Count == 0)
			return;

		var volunteerUserIds = volunteerIds.Select(id => UserId.Create(id).GetValueOrThrow()).ToList();
		var volunteerUsersById = (await dbContext.GetOrCreateUsersAsync(volunteerUserIds, cancellationToken))
			.ToDictionary(u => u.Id);

		var profileMap = await keycloakUserService.GetUserProfilesAsync(volunteerIds, cancellationToken);

		var messages = new List<EmailMessage>(volunteerIds.Count);
		foreach (var volunteerId in volunteerIds)
		{
			if (!profileMap.TryGetValue(volunteerId, out var volunteer))
				continue;

			var volunteerUser = volunteerUsersById[UserId.Create(volunteerId).GetValueOrThrow()];
			var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser.PreferredLanguage);

			var content = emailTemplateRenderer.Render(
				EmailTemplateKind.OpportunityUpdated,
				volunteerLanguage,
				new Dictionary<string, string>
				{
					["VolunteerName"] = volunteer.FirstName ?? volunteer.Username,
					["OpportunityTitle"] = opportunity.TitleDe,
				});

			messages.Add(new EmailMessage(volunteer.Email, content.Subject, content.Body, volunteerId.ToString()));
		}

		if (messages.Count > 0)
			await emailService.SendBatchAsync(messages, cancellationToken);
	}
}
