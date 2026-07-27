using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Reports.ListFlaggedTargets.v1;

internal sealed class ListFlaggedTargetsQueryHandler(
	IAdminReportReadRepository readRepository)
	: IQueryHandler<ListFlaggedTargetsQuery, PagedList<FlaggedTargetSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<FlaggedTargetSummary>> Handle(
		ListFlaggedTargetsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await readRepository.GetFlaggedTargetsPagedAsync(pageNumber, pageSize, cancellationToken);
	}
}
