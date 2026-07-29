using System.Collections.Concurrent;
using Microsoft.Extensions.Primitives;

namespace Application.Common.Caching;

// Cache entries are tagged with a per-category IChangeToken when written (see
// CachingPipelineBehavior). Invalidating a category cancels its token, which evicts every
// entry tagged with it from IMemoryCache immediately, regardless of the entry's own key -
// this is what lets a domain event invalidate "the cache for the entity" without needing to
// enumerate or pattern-match the individual cache keys that happen to exist for it.
internal sealed class CacheCategoryTokenStore : ICacheInvalidator, ICacheCategoryTokenProvider
{
	private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokenSources = new();

	public IChangeToken GetToken(string category)
	{
		var cts = _tokenSources.GetOrAdd(category, static _ => new CancellationTokenSource());
		return new CancellationChangeToken(cts.Token);
	}

	public void Invalidate(string category)
	{
		if (_tokenSources.TryRemove(category, out var cts))
		{
			cts.Cancel();
			cts.Dispose();
		}
	}

	public void InvalidateAll()
	{
		foreach (var category in _tokenSources.Keys.ToList())
			Invalidate(category);
	}
}
