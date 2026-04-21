using Application.Common.Messaging;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.GetEngagements.v1;

public sealed record GetEngagementsQuery(VolunteerOpportunityId OpportunityId)
    : IQuery<List<EngagementSummary>>;
