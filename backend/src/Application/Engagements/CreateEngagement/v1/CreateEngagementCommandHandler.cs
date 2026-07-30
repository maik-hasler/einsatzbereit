using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.CreateEngagement.v1;

internal sealed class CreateEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	IUnsubscribeLinkBuilder unsubscribeLinkBuilder)
	: ICommandHandler<CreateEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CreateEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			request.OpportunityId, cancellationToken);

		if (opportunity is null)
			throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity with id '{request.OpportunityId.Value}' was not found."));

		var alreadySignedUp = await dbContext.HasEngagementAsync(
			request.VolunteerId, request.OpportunityId, request.TimeSlotId, cancellationToken);

		if (alreadySignedUp)
			throw new ResultFailureException(Error.Conflict("Engagement.AlreadySignedUp", "Conflict: you are already signed up for this opportunity."));

		if (request.TimeSlotId is not null)
		{
			var timeSlot = opportunity.TimeSlots.FirstOrDefault(ts => ts.Id == request.TimeSlotId);
			if (timeSlot is null)
				throw new ResultFailureException(Error.Validation("Engagement.TimeSlotNotInOpportunity", "The selected time slot does not belong to this opportunity."));

			var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(
				request.TimeSlotId.Value, cancellationToken);
			if (timeSlot.MaxParticipants is int max && activeCount >= max)
				throw new ResultFailureException(Error.Conflict("Engagement.TimeSlotFull", "Conflict: this time slot has reached its capacity and cannot accept more sign-ups."));
		}

		var existingTerminal = await dbContext.GetTerminalEngagementAsync(
			request.VolunteerId, request.OpportunityId, request.TimeSlotId, cancellationToken);

		Engagement engagement;
		if (existingTerminal is not null)
		{
			existingTerminal.Reactivate(request.TimeSlotId, request.Message).ThrowIfFailure();
			engagement = existingTerminal;
		}
		else
		{
			engagement = request.TimeSlotId is not null
				? Engagement.CreateSlotSignUp(request.OpportunityId, request.VolunteerId, request.TimeSlotId.Value)
				: Engagement.CreateIndividualContact(request.OpportunityId, request.VolunteerId, request.Message
					?? throw new ResultFailureException(Error.Validation("Engagement.MessageRequired", "Message is required for individual contact."))).GetValueOrThrow();

			await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		}

		var members = await keycloakOrganizationService
			.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);

		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var notification = Notification.Create(
				UserId.Create(organizer.UserId).GetValueOrThrow(),
				NotificationKind.EngagementCreated,
				engagement.Id.Value);

			await dbContext.Notifications.AddAsync(notification, cancellationToken);
		}

		var volunteer = await keycloakUserService.GetUserAsync(request.VolunteerId.Value, cancellationToken);
		var volunteerName = volunteer.FirstName ?? volunteer.Username;
		var isSlotSignUp = request.TimeSlotId is not null;

		var volunteerUser = await dbContext.Users.FindAsync(request.VolunteerId, cancellationToken);
		var volunteerLanguage = SupportedLanguages.Resolve(volunteerUser?.PreferredLanguage);

		var volunteerContent = emailTemplateRenderer.Render(
			isSlotSignUp ? EmailTemplateKind.EngagementWaitlisted : EmailTemplateKind.EngagementRequestReceived,
			volunteerLanguage,
			new Dictionary<string, string>
			{
				["VolunteerName"] = volunteerName,
				["OpportunityTitle"] = opportunity.Title,
			});

		// Never gated by preference (#1055): this is the direct, synchronous
		// response to the volunteer's own just-submitted action, not a repeatable
		// notification about someone else's activity - equivalent to an order
		// receipt, which platforms conventionally don't let users opt out of.
		await emailService.SendAsync(volunteer.Email, volunteerContent.Subject, volunteerContent.Body, cancellationToken);

		var organizerIds = members
			.Where(m => m.IsOrganisator)
			.Select(m => UserId.Create(m.UserId).GetValueOrThrow())
			.ToList();
		var organizerUsersById = (await dbContext.GetOrCreateUsersAsync(organizerIds, cancellationToken))
			.ToDictionary(u => u.Id);

		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var organizerId = UserId.Create(organizer.UserId).GetValueOrThrow();
			var organizerUser = organizerUsersById[organizerId];

			if (!organizerUser.IsSubscribedTo(EmailNotificationType.NewSignUp))
				continue;

			var organizerName = organizer.FirstName ?? organizer.Username;
			var organizerLanguage = SupportedLanguages.Resolve(organizerUser.PreferredLanguage);

			var organizerContent = emailTemplateRenderer.Render(
				EmailTemplateKind.EngagementSignupNotifyOrganizer,
				organizerLanguage,
				new Dictionary<string, string>
				{
					["OrganizerName"] = organizerName,
					["VolunteerName"] = volunteerName,
					["OpportunityTitle"] = opportunity.Title,
				});

			var unsubscribeUrl = unsubscribeLinkBuilder.Build(
				organizerId, organizerUser.UnsubscribeToken, EmailNotificationType.NewSignUp);

			await emailService.SendAsync(
				organizer.Email,
				organizerContent.Subject,
				EmailFooter.Append(organizerContent.Body, unsubscribeUrl),
				cancellationToken);
		}

		return engagement;
	}
}
