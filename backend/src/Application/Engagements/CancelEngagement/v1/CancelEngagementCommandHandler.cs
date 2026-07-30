using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CancelEngagement.v1;

internal sealed class CancelEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer)
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

		await EngagementCancellationHelper.CancelAndNotifyAsync(
			dbContext,
			keycloakUserService,
			emailService,
			emailTemplateRenderer,
			engagement,
			opportunity.Title,
			request.Reason,
			cancellationToken);

		return engagement;
	}
}
