using Application.Common.Messaging;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.GetEngagements.v1;

public sealed record GetEngagementsQuery(VolunteerOpportunityId OpportunityId, UserId RequestingUserId)
	: IQuery<List<EngagementSummary>>;
