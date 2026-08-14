using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Organizations.GetPublicOrganizations.v1;

public sealed record GetPublicOrganizationsQuery(
	int PageNumber,
	int PageSize,
	string? Search)
	: IQuery<PagedList<PublicOrganizationSummary>>;
