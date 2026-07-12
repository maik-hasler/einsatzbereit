using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Engagements.GetMyEngagements.v1;

internal sealed class GetMyEngagementsQueryHandler(
	IEngagementReadRepository readRepository)
	: IQueryHandler<GetMyEngagementsQuery, PagedList<EngagementSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<EngagementSummary>> Handle(
		GetMyEngagementsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await readRepository.GetByVolunteerAsync(
			request.VolunteerId,
			request.Upcoming,
			pageNumber,
			pageSize,
			cancellationToken);
	}
}
