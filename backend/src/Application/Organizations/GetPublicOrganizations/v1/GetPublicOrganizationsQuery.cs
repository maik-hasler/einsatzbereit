using Application.Common.Caching;
using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Organizations.GetPublicOrganizations.v1;

public sealed record GetPublicOrganizationsQuery(
	int PageNumber,
	int PageSize,
	string? Search)
	: ICachedQuery<PagedList<PublicOrganizationSummary>>
{
	public string CacheKey =>
		string.Join('|', "organizations:directory", PageNumber, PageSize, Search?.Trim().ToLowerInvariant() ?? string.Empty);

	public IReadOnlyCollection<string> CacheCategories { get; } = [CacheCategory.Organizations, CacheCategory.VolunteerOpportunities];

	public TimeSpan Expiration => CachingDefaults.Expiration;
}
