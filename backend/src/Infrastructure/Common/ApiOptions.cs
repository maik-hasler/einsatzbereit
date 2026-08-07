namespace Infrastructure.Common;

internal sealed class ApiOptions
{
	public string PublicBaseUrl { get; set; } = "http://localhost:5000";

	// Derived from Cors:Origins (ApiOptionsSetup), same source as
	// GetSitemapEndpoint/GetEngagementCalendarEndpoint/UnsubscribeEndpoint's own
	// frontend-base-URL lookups - there is no dedicated "frontend base URL"
	// setting in this codebase (#1725).
	public string FrontendBaseUrl { get; set; } = "http://localhost:4321";
}
