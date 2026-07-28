using Application.Common.Messaging;
using Application.Common.Pagination;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.GetEngagements.v1;

public sealed record GetEngagementsQuery(
	VolunteerOpportunityId OpportunityId,
	UserId RequestingUserId,
	int PageNumber,
	int PageSize)
	: IQuery<PagedList<EngagementSummary>>;
