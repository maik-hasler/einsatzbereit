using Application.Common.Caching;
using AwesomeAssertions;

namespace Application.UnitTests.Common.Caching;

public class CacheCategoryTokenStoreTests
{
	[Test]
	public void GetToken_ShouldReturnTokenThatHasNotFired_WhenCategoryIsUntouched()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();

		// Act
		var token = store.GetToken("Category");

		// Assert
		token.HasChanged.Should().BeFalse();
	}

	[Test]
	public void Invalidate_ShouldFireTheToken_ThatWasIssuedForTheSameCategory()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();
		var token = store.GetToken("Category");

		// Act
		store.Invalidate("Category");

		// Assert
		token.HasChanged.Should().BeTrue();
	}

	[Test]
	public void Invalidate_ShouldNotFireTokens_IssuedForADifferentCategory()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();
		var token = store.GetToken("Category");

		// Act
		store.Invalidate("OtherCategory");

		// Assert
		token.HasChanged.Should().BeFalse();
	}

	[Test]
	public void Invalidate_ShouldNotThrow_WhenCategoryHasNoTokensYet()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();

		// Act
		var act = () => store.Invalidate("NeverRequested");

		// Assert
		act.Should().NotThrow();
	}

	[Test]
	public void GetToken_ShouldReturnFreshUnfiredToken_AfterThePriorTokenWasInvalidated()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();
		store.GetToken("Category");
		store.Invalidate("Category");

		// Act
		var token = store.GetToken("Category");

		// Assert
		token.HasChanged.Should().BeFalse();
	}

	[Test]
	public void InvalidateAll_ShouldFireTokens_ForEveryCategoryEverRequested()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();
		var tokenA = store.GetToken("CategoryA");
		var tokenB = store.GetToken("CategoryB");

		// Act
		store.InvalidateAll();

		// Assert
		tokenA.HasChanged.Should().BeTrue();
		tokenB.HasChanged.Should().BeTrue();
	}

	[Test]
	public void InvalidateAll_ShouldNotThrow_WhenNoCategoryHasBeenRequestedYet()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();

		// Act
		var act = () => store.InvalidateAll();

		// Assert
		act.Should().NotThrow();
	}

	[Test]
	public void GetToken_ShouldReturnFreshUnfiredToken_AfterInvalidateAll()
	{
		// Arrange
		var store = new CacheCategoryTokenStore();
		store.GetToken("Category");
		store.InvalidateAll();

		// Act
		var token = store.GetToken("Category");

		// Assert
		token.HasChanged.Should().BeFalse();
	}
}
