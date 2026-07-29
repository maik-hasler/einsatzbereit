namespace Application.Common.Messaging;

public interface ICachedQuery<out TResponse> : IQuery<TResponse>
{
	string CacheKey { get; }

	IReadOnlyCollection<string> CacheCategories { get; }

	TimeSpan Expiration { get; }
}
