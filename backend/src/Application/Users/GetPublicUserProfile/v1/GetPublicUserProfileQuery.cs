using Application.Common.Caching;
using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.GetPublicUserProfile.v1;

public sealed record GetPublicUserProfileQuery(UserId UserId)
	: ICachedQuery<PublicUserProfileResponse?>
{
	public string CacheKey => $"users:profile:{UserId.Value}";

	public IReadOnlyCollection<string> CacheCategories { get; } = [CacheCategory.Users];

	public TimeSpan Expiration => CachingDefaults.Expiration;
}
