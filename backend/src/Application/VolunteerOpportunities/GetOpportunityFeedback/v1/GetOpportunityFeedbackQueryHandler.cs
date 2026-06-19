using Application.Common.Messaging;
using Application.Engagements;

namespace Application.VolunteerOpportunities.GetOpportunityFeedback.v1;

internal sealed class GetOpportunityFeedbackQueryHandler(
	IEngagementReadRepository engagementReadRepository)
	: IQueryHandler<GetOpportunityFeedbackQuery, OpportunityFeedbackSummary>
{
	public async ValueTask<OpportunityFeedbackSummary> Handle(
		GetOpportunityFeedbackQuery request,
		CancellationToken cancellationToken = default)
	{
		return await engagementReadRepository.GetFeedbackByOpportunityAsync(
			request.OpportunityId,
			cancellationToken);
	}
}
