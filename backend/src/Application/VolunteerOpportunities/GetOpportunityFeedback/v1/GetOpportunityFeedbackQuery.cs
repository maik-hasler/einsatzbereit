using Application.Common.Messaging;
using Application.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.GetOpportunityFeedback.v1;

public sealed record GetOpportunityFeedbackQuery(
	VolunteerOpportunityId OpportunityId,
	UserId RequestingUserId,
	int PageNumber,
	int PageSize)
	: IQuery<OpportunityFeedbackSummary>;
