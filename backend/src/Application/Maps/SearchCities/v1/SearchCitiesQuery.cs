using Application.Common.Geocoding;
using Application.Common.Messaging;

namespace Application.Maps.SearchCities.v1;

public sealed record SearchCitiesQuery(string Query, string Language) : IQuery<IReadOnlyList<CitySuggestion>>;
