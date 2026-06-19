using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;
using Domain.Users;

namespace Application.Engagements.CreateEngagement.v1;

internal sealed class CreateEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService)
	: ICommandHandler<CreateEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CreateEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			request.OpportunityId, cancellationToken);

		if (opportunity is null)
			throw new DomainException($"Volunteer opportunity with id '{request.OpportunityId.Value}' was not found.");

		var alreadySignedUp = await dbContext.HasEngagementAsync(
			request.VolunteerId, request.OpportunityId, cancellationToken);

		if (alreadySignedUp)
			throw new DomainException("Conflict: you are already signed up for this opportunity.");

		var engagement = request.TimeSlotId is not null
			? Engagement.CreateWaitlistSignUp(request.OpportunityId, request.VolunteerId, request.TimeSlotId.Value)
			: Engagement.CreateIndividualContact(request.OpportunityId, request.VolunteerId, request.Message
				?? throw new DomainException("Message is required for individual contact."));

		await dbContext.Engagements.AddAsync(engagement, cancellationToken);

		var members = await keycloakOrganizationService
			.GetMembersAsync(opportunity.OrganizationId.Value, cancellationToken);

		foreach (var organizer in members.Where(m => m.IsOrganisator))
		{
			var notification = Notification.Create(
				new UserId(organizer.UserId),
				NotificationKind.EngagementCreated,
				engagement.Id.Value);

			await dbContext.Notifications.AddAsync(notification, cancellationToken);
		}

		try
		{
			var volunteer = await keycloakUserService.GetUserAsync(
				request.VolunteerId.Value, cancellationToken);

			var greeting = volunteer.FirstName ?? volunteer.Username;
			var title = opportunity.Title;

			// Email to volunteer
			if (request.TimeSlotId is not null)
			{
				await emailService.SendAsync(
					volunteer.Email,
					$"You're on the waitlist for \"{title}\"",
					$"Hello {greeting},\n\n" +
					$"You've been added to the waitlist for \"{title}\".\n\n" +
					$"The organiser will confirm your spot soon.\n\nEinsatzbereit",
					cancellationToken);
			}
			else
			{
				await emailService.SendAsync(
					volunteer.Email,
					$"Your interest in \"{title}\" has been registered",
					$"Hello {greeting},\n\n" +
					$"Thank you for expressing your interest in \"{title}\".\n\n" +
					$"The organiser will be in touch with you.\n\nEinsatzbereit",
					cancellationToken);
			}

			// Email to organisators
			foreach (var organizer in members.Where(m => m.IsOrganisator))
			{
				await emailService.SendAsync(
					organizer.Email,
					$"New sign-up: {greeting} joined \"{title}\"",
					$"Hello {organizer.FirstName ?? organizer.Username},\n\n" +
					$"{volunteer.FirstName} {volunteer.LastName} ({volunteer.Email}) has signed up for \"{title}\".\n\n" +
					$"Log in to Einsatzbereit to manage applications.\n\nEinsatzbereit",
					cancellationToken);
			}
		}
		catch
		{
			// never fail a request due to email delivery
		}

		return engagement;
	}
}
