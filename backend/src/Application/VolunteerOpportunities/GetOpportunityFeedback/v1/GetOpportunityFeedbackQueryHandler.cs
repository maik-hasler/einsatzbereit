using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Primitives;

namespace Application.VolunteerOpportunities.GetOpportunityFeedback.v1;

internal sealed class GetOpportunityFeedbackQueryHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository)
	: IQueryHandler<GetOpportunityFeedbackQuery, OpportunityFeedbackSummary>
{
	private const int MaxPageSize = 100;

	public async ValueTask<OpportunityFeedbackSummary> Handle(
		GetOpportunityFeedbackQuery request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound(
				"VolunteerOpportunity.NotFound",
				$"Volunteer opportunity '{request.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsMemberAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await engagementReadRepository.GetFeedbackByOpportunityAsync(
			request.OpportunityId,
			pageNumber,
			pageSize,
			cancellationToken);
	}
}
