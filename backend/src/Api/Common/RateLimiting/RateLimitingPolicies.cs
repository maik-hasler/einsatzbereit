namespace Api.Common.RateLimiting;

public static class RateLimitingPolicies
{
	public const string Read = "rate-limit-read";
	public const string Write = "rate-limit-write";
	public const string MapTiles = "rate-limit-map-tiles";
}
