using Application.Common.Geocoding;
using Application.Common.Messaging;

namespace Application.Maps.SearchCities.v1;

internal sealed class SearchCitiesQueryHandler(
	IGeocodingService geocodingService)
	: IQueryHandler<SearchCitiesQuery, IReadOnlyList<CitySuggestion>>
{
	public async ValueTask<IReadOnlyList<CitySuggestion>> Handle(
		SearchCitiesQuery request,
		CancellationToken cancellationToken = default) =>
		await geocodingService.SearchCitiesAsync(request.Query, cancellationToken);
}
