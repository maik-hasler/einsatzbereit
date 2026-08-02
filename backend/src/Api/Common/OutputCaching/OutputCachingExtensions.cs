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

			cache.AddPolicy(OutputCachingPolicies.VolunteerOpportunityListing, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.ShortPublicReadSeconds))
					.Tag(OutputCachingPolicies.VolunteerOpportunityListingTag));
		});
	}

	// Called by every command that changes what the public volunteer-opportunity
	// listing should show (see OutputCachingPolicies.VolunteerOpportunityListingTag
	// for the full list) so a write is reflected on the very next read of the listing
	// instead of waiting out the policy's Expire() duration (#1543).
	public static ValueTask EvictVolunteerOpportunityListingCacheAsync(
		this IOutputCacheStore store,
		CancellationToken cancellationToken) =>
		store.EvictByTagAsync(OutputCachingPolicies.VolunteerOpportunityListingTag, cancellationToken);
}
