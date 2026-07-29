using Application.Achievements.BadgeCatalog;
using Application.Common.Caching;
using Application.Common.Messaging;

namespace Application.Achievements.GetBadgeCatalog.v1;

public sealed record GetBadgeCatalogQuery
	: ICachedQuery<List<BadgeCatalogEntry>>
{
	public string CacheKey => "achievements:badge-catalog";

	public IReadOnlyCollection<string> CacheCategories { get; } = [CacheCategory.BadgeCatalog];

	public TimeSpan Expiration => CachingDefaults.Expiration;
}
