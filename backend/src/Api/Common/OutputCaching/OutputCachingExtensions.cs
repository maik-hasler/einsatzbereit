using Microsoft.AspNetCore.OutputCaching;

namespace Api.Common.OutputCaching;

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

			cache.AddPolicy(OutputCachingPolicies.LongPublicReadByLanguage, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.LongPublicReadSeconds))
					.SetVaryByHeader("X-Language"));

			cache.AddPolicy(OutputCachingPolicies.ShortPublicRead, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.ShortPublicReadSeconds)));

			cache.AddPolicy(OutputCachingPolicies.HealthCheck, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.HealthCheckSeconds)));

			cache.AddPolicy(OutputCachingPolicies.VolunteerOpportunityListing, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.ShortPublicReadSeconds))
					.Tag(OutputCachingPolicies.VolunteerOpportunityListingTag));

			// Its own policy rather than reusing VolunteerOpportunityListing above - the
			// result here depends on the caller's X-Timezone, which the plain listing
			// doesn't vary by and shouldn't start fragmenting its cache over (#2203).
			cache.AddPolicy(OutputCachingPolicies.VolunteerOpportunityDateAvailability, policy =>
				policy.Expire(TimeSpan.FromSeconds(options.ShortPublicReadSeconds))
					.SetVaryByHeader("X-Timezone")
					.Tag(OutputCachingPolicies.VolunteerOpportunityListingTag));
		});
	}

	public static ValueTask EvictVolunteerOpportunityListingCacheAsync(
		this IOutputCacheStore store,
		CancellationToken cancellationToken) =>
		store.EvictByTagAsync(OutputCachingPolicies.VolunteerOpportunityListingTag, cancellationToken);
}
