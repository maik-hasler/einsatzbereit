using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.CancelEngagement.v1;
using Application.Engagements.Common;
using Domain.Primitives;

namespace Application.Engagements.BulkCancelEngagements.v1;

internal sealed class BulkCancelEngagementsCommandHandler(
	IApplicationDbContext dbContext,
	ISender sender)
	: ICommandHandler<BulkCancelEngagementsCommand, BulkEngagementActionResult>
{
	public async ValueTask<BulkEngagementActionResult> Handle(
		BulkCancelEngagementsCommand request,
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
			// The nested CancelEngagementCommand re-derives ownership from the
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
				var cancelled = await sender.Send(
					new CancelEngagementCommand(engagementId, request.RequestingUserId, request.Reason),
					cancellationToken);
				succeeded.Add(new BulkEngagementActionSuccess(cancelled.Id.Value, cancelled.Status.ToString(), cancelled.CancellationReason));
			}
			catch (ResultFailureException ex)
			{
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, ex.Error.Code, ex.Error.Description));
			}
		}

		return new BulkEngagementActionResult(succeeded, failed);
	}
}
