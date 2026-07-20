using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Organizations.ListOrganizations.v1;

public sealed record ListOrganizationsQuery(
	int PageNumber,
	int PageSize)
	: IQuery<PagedList<AdminOrganizationSummary>>;
