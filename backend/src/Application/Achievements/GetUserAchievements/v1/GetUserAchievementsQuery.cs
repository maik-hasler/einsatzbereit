using Application.Common.Caching;
using Application.Common.Messaging;
using Domain.Users;

namespace Application.Achievements.GetUserAchievements.v1;

public sealed record GetUserAchievementsQuery(UserId UserId)
	: ICachedQuery<List<AchievementSummary>>
{
	public string CacheKey => $"achievements:user:{UserId.Value}";

	public IReadOnlyCollection<string> CacheCategories { get; } = [CacheCategory.Achievements];

	public TimeSpan Expiration => CachingDefaults.Expiration;
}
