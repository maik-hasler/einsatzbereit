using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Organizations.ListOrganizations.v1;

public sealed record ListOrganizationsQuery(
	int PageNumber,
	int PageSize,
	string? Search = null,
	bool? Deleted = null,
	bool? Flagged = null)
	: IQuery<PagedList<AdminOrganizationSummary>>;
