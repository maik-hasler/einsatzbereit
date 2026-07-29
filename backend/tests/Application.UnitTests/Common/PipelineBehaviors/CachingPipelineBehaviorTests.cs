using Application.Common.Caching;
using Application.Common.Messaging;
using Application.Common.PipelineBehaviors;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace Application.UnitTests.Common.PipelineBehaviors;

public class CachingPipelineBehaviorTests
{
	[Test]
	public async Task Handle_ShouldCallNext_WhenCacheIsEmpty(
		CancellationToken cancellationToken)
	{
		// Arrange
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var behavior = new CachingPipelineBehavior<TestQuery, string?>(cache, new CacheCategoryTokenStore());
		var nextCallCount = 0;
		ValueTask<string?> Next() { nextCallCount++; return ValueTask.FromResult<string?>("value"); }

		// Act
		var result = await behavior.Handle(new TestQuery("key-1", "TestCategory"), Next, cancellationToken);

		// Assert
		result.Should().Be("value");
		nextCallCount.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldReturnCachedValue_WithoutCallingNextAgain_WhenCacheHit(
		CancellationToken cancellationToken)
	{
		// Arrange
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var behavior = new CachingPipelineBehavior<TestQuery, string?>(cache, new CacheCategoryTokenStore());
		var query = new TestQuery("key-1", "TestCategory");
		var nextCallCount = 0;
		ValueTask<string?> Next() { nextCallCount++; return ValueTask.FromResult<string?>("value"); }

		// Act
		await behavior.Handle(query, Next, cancellationToken);
		var second = await behavior.Handle(query, Next, cancellationToken);

		// Assert
		second.Should().Be("value");
		nextCallCount.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldCallNextAgain_WhenCacheKeyDiffers(
		CancellationToken cancellationToken)
	{
		// Arrange
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var behavior = new CachingPipelineBehavior<TestQuery, string?>(cache, new CacheCategoryTokenStore());
		var nextCallCount = 0;
		ValueTask<string?> Next() { nextCallCount++; return ValueTask.FromResult<string?>("value"); }

		// Act
		await behavior.Handle(new TestQuery("key-1", "TestCategory"), Next, cancellationToken);
		await behavior.Handle(new TestQuery("key-2", "TestCategory"), Next, cancellationToken);

		// Assert
		nextCallCount.Should().Be(2);
	}

	[Test]
	public async Task Handle_ShouldCallNextAgain_WhenCategoryIsInvalidated(
		CancellationToken cancellationToken)
	{
		// Arrange
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var tokenStore = new CacheCategoryTokenStore();
		var behavior = new CachingPipelineBehavior<TestQuery, string?>(cache, tokenStore);
		var query = new TestQuery("key-1", "TestCategory");
		var nextCallCount = 0;
		ValueTask<string?> Next() { nextCallCount++; return ValueTask.FromResult<string?>("value"); }

		// Act
		await behavior.Handle(query, Next, cancellationToken);
		tokenStore.Invalidate("TestCategory");
		await behavior.Handle(query, Next, cancellationToken);

		// Assert
		nextCallCount.Should().Be(2);
	}

	[Test]
	public async Task Handle_ShouldNotAffectOtherCategories_WhenOneCategoryIsInvalidated(
		CancellationToken cancellationToken)
	{
		// Arrange
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var tokenStore = new CacheCategoryTokenStore();
		var behavior = new CachingPipelineBehavior<TestQuery, string?>(cache, tokenStore);
		var query = new TestQuery("key-1", "TestCategory");
		var nextCallCount = 0;
		ValueTask<string?> Next() { nextCallCount++; return ValueTask.FromResult<string?>("value"); }

		// Act
		await behavior.Handle(query, Next, cancellationToken);
		tokenStore.Invalidate("SomeOtherCategory");
		await behavior.Handle(query, Next, cancellationToken);

		// Assert
		nextCallCount.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldCallNextEveryTime_WhenResultIsNull(
		CancellationToken cancellationToken)
	{
		// Arrange - a null result (e.g. "not found") is never cached, so a repeated
		// lookup for a still-missing entity isn't stuck returning null forever.
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var behavior = new CachingPipelineBehavior<TestQuery, string?>(cache, new CacheCategoryTokenStore());
		var query = new TestQuery("key-1", "TestCategory");
		var nextCallCount = 0;
		ValueTask<string?> Next() { nextCallCount++; return ValueTask.FromResult<string?>(null); }

		// Act
		await behavior.Handle(query, Next, cancellationToken);
		await behavior.Handle(query, Next, cancellationToken);

		// Assert
		nextCallCount.Should().Be(2);
	}

	private sealed record TestQuery(string CacheKey, string Category) : ICachedQuery<string?>
	{
		public IReadOnlyCollection<string> CacheCategories { get; } = [Category];

		public TimeSpan Expiration => TimeSpan.FromMinutes(5);
	}
}
