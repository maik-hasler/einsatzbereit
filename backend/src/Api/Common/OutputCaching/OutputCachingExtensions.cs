using Microsoft.AspNetCore.OutputCaching;

namespace Api.Common.OutputCaching;

// Policies are applied only to AllowAnonymous endpoints whose response never varies by
// caller (see the .CacheOutput(...) call sites) - the default OutputCache cache key is
// the request path + query string, with no per-user variance, so applying it to an
// authenticated or caller-personalized endpoint would risk serving one user's cached
// response to another (#1391).
internal static class OutputCachingExtensions
{
	public static IServiceCollection AddOutputCachingPolicies(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var options = configuration
			.GetSection("OutputCaching")
			.Get<OutputCachingOptions>() ?? new OutputCachingOptions();

		return services.AddOutputCache(cache =>
		{
			cache.AddPolicy(OutputCachingPolicies.LongPublicRead, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.LongPublicReadSeconds)));

			cache.AddPolicy(OutputCachingPolicies.ShortPublicRead, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.ShortPublicReadSeconds)));
		});
	}
}
