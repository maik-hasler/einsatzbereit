using Application.Common.Messaging;
using Application.Common.Pagination;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.GetEngagements.v1;

public sealed record GetEngagementsQuery(
	VolunteerOpportunityId OpportunityId,
	UserId RequestingUserId,
	int PageNumber,
	int PageSize,
	EngagementStatus? Status = null,
	TimeSlotId? TimeSlotId = null,
	string? Search = null)
	: IQuery<PagedList<EngagementSummary>>;
