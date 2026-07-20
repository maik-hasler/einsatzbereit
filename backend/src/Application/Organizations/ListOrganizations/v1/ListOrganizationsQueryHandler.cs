using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Organizations.ListOrganizations.v1;

internal sealed class ListOrganizationsQueryHandler(
	IAdminOrganizationReadRepository readRepository)
	: IQueryHandler<ListOrganizationsQuery, PagedList<AdminOrganizationSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<AdminOrganizationSummary>> Handle(
		ListOrganizationsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await readRepository.GetPagedAsync(pageNumber, pageSize, cancellationToken);
	}
}
