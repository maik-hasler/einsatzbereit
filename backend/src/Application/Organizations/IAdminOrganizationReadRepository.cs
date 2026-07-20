using Application.Common.Pagination;
using Application.Organizations.ListOrganizations.v1;

namespace Application.Organizations;

public interface IAdminOrganizationReadRepository
{
	ValueTask<PagedList<AdminOrganizationSummary>> GetPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);
}
