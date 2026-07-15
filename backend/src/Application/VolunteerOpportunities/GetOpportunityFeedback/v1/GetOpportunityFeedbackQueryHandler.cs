using Application.Common.Authorization;
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
	public async ValueTask<OpportunityFeedbackSummary> Handle(
		GetOpportunityFeedbackQuery request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId.Value}' not found.");

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		return await engagementReadRepository.GetFeedbackByOpportunityAsync(
			request.OpportunityId,
			cancellationToken);
	}
}
