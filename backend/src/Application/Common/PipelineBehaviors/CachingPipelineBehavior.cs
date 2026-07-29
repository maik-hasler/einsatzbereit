using Application.Common.Caching;
using Application.Common.Messaging;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Common.PipelineBehaviors;

internal sealed class CachingPipelineBehavior<TQuery, TResponse>(
	IMemoryCache cache,
	ICacheCategoryTokenProvider tokenProvider)
	: IPipelineBehavior<TQuery, TResponse>
	where TQuery : ICachedQuery<TResponse>
{
	public async ValueTask<TResponse> Handle(
		TQuery request,
		Func<ValueTask<TResponse>> next,
		CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(request.CacheKey, out TResponse? cached) && cached is not null)
			return cached;

		var response = await next().ConfigureAwait(false);

		if (response is not null)
		{
			var options = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = request.Expiration,
			};

			foreach (var category in request.CacheCategories)
				options.AddExpirationToken(tokenProvider.GetToken(category));

			cache.Set(request.CacheKey, response, options);
		}

		return response;
	}
}
