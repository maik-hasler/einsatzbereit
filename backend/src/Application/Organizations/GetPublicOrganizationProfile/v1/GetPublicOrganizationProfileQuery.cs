using Application.Common.Caching;
using Application.Common.Messaging;

namespace Application.Organizations.GetPublicOrganizationProfile.v1;

public sealed record GetPublicOrganizationProfileQuery(Guid OrganizationId)
	: ICachedQuery<PublicOrganizationProfileResponse?>
{
	public string CacheKey => $"organizations:profile:{OrganizationId}";

	public IReadOnlyCollection<string> CacheCategories { get; } = [CacheCategory.Organizations, CacheCategory.VolunteerOpportunities];

	public TimeSpan Expiration => CachingDefaults.Expiration;
}
