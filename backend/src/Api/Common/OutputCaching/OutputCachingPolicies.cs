namespace Api.Common.OutputCaching;

public static class OutputCachingPolicies
{
	public const string LongPublicRead = "output-cache-long-public-read";
	public const string ShortPublicRead = "output-cache-short-public-read";

	public const string LongPublicReadByLanguage = "output-cache-long-public-read-by-language";

	public const string HealthCheck = "output-cache-health-check";

	public const string VolunteerOpportunityListing = "output-cache-volunteer-opportunity-listing";
	public const string VolunteerOpportunityDateAvailability = "output-cache-volunteer-opportunity-date-availability";

	public const string VolunteerOpportunityListingTag = "volunteer-opportunity-listing";
}
