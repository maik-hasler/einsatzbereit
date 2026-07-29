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
}
