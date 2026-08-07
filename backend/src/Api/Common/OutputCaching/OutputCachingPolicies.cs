namespace Api.Common.OutputCaching;

public static class OutputCachingPolicies
{
	public const string LongPublicRead = "output-cache-long-public-read";
	public const string ShortPublicRead = "output-cache-short-public-read";

	// Same expiry as LongPublicRead, but for a response that varies by the caller's
	// X-Language header (e.g. SearchCities) - LongPublicRead's cache key is only the
	// request path + query string (see the OutputCachingExtensions.cs comment), so a
	// language-dependent endpoint using it as-is could serve a response cached for one
	// language to a caller requesting another (#1731).
	public const string LongPublicReadByLanguage = "output-cache-long-public-read-by-language";

	// A few seconds only, not ShortPublicReadSeconds - /health backs the deploy gate's
	// and docker-compose's readiness probes, both of which need to observe a real
	// dependency outage within a handful of seconds, not up to a full minute (#1172).
	public const string HealthCheck = "output-cache-health-check";

	// Same expiry as ShortPublicRead, but tagged separately so a write that changes
	// what the public volunteer-opportunity listing should show can evict just this
	// endpoint's cache entries via IOutputCacheStore.EvictByTagAsync instead of
	// waiting out ShortPublicReadSeconds of staleness (#1543).
	public const string VolunteerOpportunityListing = "output-cache-volunteer-opportunity-listing";

	// Tag applied to every response cached under VolunteerOpportunityListing. Evicted
	// by any command that changes volunteer-opportunity visibility/content (create,
	// update, publish, unpublish, cancel, delete, restore, time-slot changes, banner
	// changes) or the participant counts the listing surfaces (sign-up, withdraw,
	// cancel) - see OutputCachingExtensions.EvictVolunteerOpportunityListingCacheAsync.
	public const string VolunteerOpportunityListingTag = "volunteer-opportunity-listing";
}
