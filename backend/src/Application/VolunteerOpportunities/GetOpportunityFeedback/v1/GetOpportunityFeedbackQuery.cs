using Application.Common.Messaging;
using Application.Engagements;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.GetOpportunityFeedback.v1;

public sealed record GetOpportunityFeedbackQuery(
	VolunteerOpportunityId OpportunityId)
	: IQuery<OpportunityFeedbackSummary>;
