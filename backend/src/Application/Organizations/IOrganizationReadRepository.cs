using Application.Common.Pagination;
using Application.Organizations.GetPublicOrganizations.v1;

namespace Application.Organizations;

public interface IOrganizationReadRepository
{
	ValueTask<PagedList<PublicOrganizationSummary>> GetPagedPublicSummariesAsync(
		OrganizationFilter filter,
		CancellationToken cancellationToken = default);
}
