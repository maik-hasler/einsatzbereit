using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.UndoCheckInEngagement.v1;

internal sealed class UndoCheckInEngagementCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<UndoCheckInEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		UndoCheckInEngagementCommand request,
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

		engagement.UndoCheckIn().ThrowIfFailure();

		return engagement;
	}
}
