using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Primitives;

namespace Application.Engagements.CancelEngagement.v1;

internal sealed class CancelEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService)
	: ICommandHandler<CancelEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CancelEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{engagement.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		engagement.Cancel(request.Reason).ThrowIfFailure();

		var notification = Notification.Create(
			engagement.VolunteerId!.Value,
			NotificationKind.EngagementCancelled,
			engagement.Id.Value);

		await dbContext.Notifications.AddAsync(notification, cancellationToken);
		var volunteer = await keycloakUserService.GetUserAsync(engagement.VolunteerId!.Value.Value, cancellationToken);

		var reasonText = string.IsNullOrWhiteSpace(request.Reason)
			? string.Empty
			: $"\n\nReason: {request.Reason}";

		await emailService.SendAsync(
			volunteer.Email,
			"Your engagement has been cancelled",
			$"Hello {volunteer.FirstName ?? volunteer.Username},\n\n" +
			$"Unfortunately your application for \"{opportunity.Title}\" has been cancelled.{reasonText}\n\n" +
			$"We hope to see you at another opportunity.\n\nEinsatzbereit",
			cancellationToken);

		return engagement;
	}
}
