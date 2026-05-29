using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

internal sealed class GetVolunteerOpportunitiesQueryHandler(
	IVolunteerOpportunityReadRepository readRepository)
	: IQueryHandler<GetVolunteerOpportunitiesQuery, PagedList<VolunteerOpportunitySummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<VolunteerOpportunitySummary>> Handle(
		GetVolunteerOpportunitiesQuery request,
		CancellationToken cancellationToken = default)
	{
		// Normalize paging so a non-positive page number can never produce a
		// negative SQL OFFSET (#362) and an unbounded page size is capped (#363).
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		var filter = new VolunteerOpportunityFilter(
			pageNumber,
			pageSize,
			request.Search,
			request.City,
			request.Occurrence,
			request.ParticipationType,
			request.IsRemote,
			request.DateFrom,
			request.DateTo,
			request.North,
			request.South,
			request.East,
			request.West,
			request.CenterLatitude,
			request.CenterLongitude,
			request.RadiusKm,
			request.Category,
			request.Tag);

		return await readRepository.GetPagedSummariesAsync(filter, cancellationToken);
	}
}
