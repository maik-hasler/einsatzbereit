using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Application.Engagements.ConfirmEngagement.v1;
using Domain.Primitives;

namespace Application.Engagements.BulkConfirmEngagements.v1;

internal sealed class BulkConfirmEngagementsCommandHandler(
	IApplicationDbContext dbContext,
	ISender sender)
	: ICommandHandler<BulkConfirmEngagementsCommand, BulkEngagementActionResult>
{
	public async ValueTask<BulkEngagementActionResult> Handle(
		BulkConfirmEngagementsCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var succeeded = new List<BulkEngagementActionSuccess>();
		var failed = new List<BulkEngagementActionFailure>();

		foreach (var engagementId in request.EngagementIds.Distinct())
		{
			// The nested ConfirmEngagementCommand re-derives ownership from the
			// engagement's own OpportunityId, independent of this route's
			// {opportunityId} segment - so without this check, an id from a
			// different opportunity the caller also organizes would silently
			// succeed instead of being rejected as out of scope for this batch.
			var engagement = await dbContext.Engagements.FindAsync(engagementId, cancellationToken);
			if (engagement is null)
			{
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, "Engagement.NotFound", $"Engagement '{engagementId.Value}' not found."));
				continue;
			}
			if (engagement.OpportunityId != request.OpportunityId)
			{
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, "Engagement.WrongOpportunity", "Engagement does not belong to this volunteer opportunity."));
				continue;
			}

			try
			{
				var confirmed = await sender.Send(
					new ConfirmEngagementCommand(engagementId, request.RequestingUserId, request.Timezone),
					cancellationToken);
				succeeded.Add(new BulkEngagementActionSuccess(confirmed.Id.Value, confirmed.Status.ToString()));
			}
			catch (ResultFailureException ex)
			{
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, ex.Error.Code, ex.Error.Description));
			}
		}

		return new BulkEngagementActionResult(succeeded, failed);
	}
}
