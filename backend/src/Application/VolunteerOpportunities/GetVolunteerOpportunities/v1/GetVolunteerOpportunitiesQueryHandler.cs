using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

internal sealed class GetVolunteerOpportunitiesQueryHandler(
	IVolunteerOpportunityReadRepository readRepository)
	: IQueryHandler<GetVolunteerOpportunitiesQuery, PagedList<VolunteerOpportunitySummary>>
{
	public async ValueTask<PagedList<VolunteerOpportunitySummary>> Handle(
		GetVolunteerOpportunitiesQuery request,
		CancellationToken cancellationToken = default)
	{
		var filter = new VolunteerOpportunityFilter(
			request.PageNumber,
			request.PageSize,
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
			request.RadiusKm);

		return await readRepository.GetPagedSummariesAsync(filter, cancellationToken);
	}
}
