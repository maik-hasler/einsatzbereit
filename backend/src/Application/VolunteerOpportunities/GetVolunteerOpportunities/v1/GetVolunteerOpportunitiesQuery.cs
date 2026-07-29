using System.Globalization;
using Application.Common.Caching;
using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

public sealed record GetVolunteerOpportunitiesQuery(
	int PageNumber,
	int PageSize,
	string? City,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	DateTimeOffset? DateFrom,
	DateTimeOffset? DateTo,
	double? North,
	double? South,
	double? East,
	double? West,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	string[]? Categories,
	string? Tag)
	: ICachedQuery<PagedList<VolunteerOpportunitySummary>>
{
	public string CacheKey =>
		string.Join(
			'|',
			"volunteer-opportunities:list",
			PageNumber,
			PageSize,
			City?.Trim().ToLowerInvariant() ?? string.Empty,
			Occurrence ?? string.Empty,
			ParticipationType ?? string.Empty,
			IsRemote?.ToString() ?? string.Empty,
			DateFrom?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
			DateTo?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
			North?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			South?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			East?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			West?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			CenterLatitude?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			CenterLongitude?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			RadiusKm?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
			Categories is { Length: > 0 } ? string.Join(',', Categories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase)) : string.Empty,
			Tag?.Trim().ToLowerInvariant() ?? string.Empty);

	public IReadOnlyCollection<string> CacheCategories { get; } = [CacheCategory.VolunteerOpportunities];

	public TimeSpan Expiration => CachingDefaults.Expiration;
}
