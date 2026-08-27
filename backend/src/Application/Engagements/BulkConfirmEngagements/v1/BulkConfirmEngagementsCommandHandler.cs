using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Primitives;

namespace Application.Engagements.BulkConfirmEngagements.v1;

internal sealed class BulkConfirmEngagementsCommandHandler(
	IApplicationDbContext dbContext,
	ISender sender,
	TimeProvider timeProvider)
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

		var requestedIds = request.EngagementIds.Distinct().ToList();
		var engagementsById = (await dbContext.GetEngagementsByIdsAsync(requestedIds, cancellationToken))
			.ToDictionary(e => e.Id);

		var succeeded = new List<BulkEngagementActionSuccess>();
		var failed = new List<BulkEngagementActionFailure>();

		foreach (var engagementId in requestedIds)
		{
			if (!engagementsById.TryGetValue(engagementId, out var engagement))
			{
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, "Engagement.NotFound", $"Engagement '{engagementId.Value}' not found."));
				continue;
			}

			// An id from a different opportunity the caller also organizes is rejected as out
			// of scope for this batch, rather than silently confirmed under this route's
			// {opportunityId} segment - this opportunity's own ownership was already checked once above.
			if (engagement.OpportunityId != request.OpportunityId)
			{
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, "Engagement.WrongOpportunity", "Engagement does not belong to this volunteer opportunity."));
				continue;
			}

			var result = await EngagementConfirmationHelper.ConfirmAsync(
				dbContext, sender, engagement, timeProvider, cancellationToken);

			if (result.IsSuccess)
				succeeded.Add(new BulkEngagementActionSuccess(result.Value.Id.Value, result.Value.Status.ToString()));
			else
				failed.Add(new BulkEngagementActionFailure(engagementId.Value, result.Error.Code, result.Error.Description));
		}

		return new BulkEngagementActionResult(succeeded, failed);
	}
}
