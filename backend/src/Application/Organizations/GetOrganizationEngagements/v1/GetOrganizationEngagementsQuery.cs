using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Engagements;
using Domain.Engagements;
using Domain.Users;

namespace Application.Organizations.GetOrganizationEngagements.v1;

public sealed record GetOrganizationEngagementsQuery(
	Guid OrganizationId,
	UserId RequestingUserId,
	int PageNumber,
	int PageSize,
	EngagementStatus? Status = null,
	string? Search = null)
	: IQuery<PagedList<EngagementSummary>>;
