using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Organizations.GetPublicOrganizations.v1;

internal sealed class GetPublicOrganizationsQueryHandler(
	IOrganizationReadRepository readRepository)
	: IQueryHandler<GetPublicOrganizationsQuery, PagedList<PublicOrganizationSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<PublicOrganizationSummary>> Handle(
		GetPublicOrganizationsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		var filter = new OrganizationFilter(pageNumber, pageSize, request.Search);

		return await readRepository.GetPagedPublicSummariesAsync(filter, cancellationToken);
	}
}
