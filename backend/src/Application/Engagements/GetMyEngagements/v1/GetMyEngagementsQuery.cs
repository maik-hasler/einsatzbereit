using Application.Common.Messaging;
using Application.Common.Pagination;
using Domain.Users;

namespace Application.Engagements.GetMyEngagements.v1;

public sealed record GetMyEngagementsQuery(
	UserId VolunteerId,
	int PageNumber,
	int PageSize,
	bool Upcoming)
	: IQuery<PagedList<EngagementSummary>>;
