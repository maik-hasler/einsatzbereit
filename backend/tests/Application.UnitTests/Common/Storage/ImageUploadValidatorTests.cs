using Application.Common.Exceptions;
using Application.Common.Storage;
using AwesomeAssertions;
using Domain.Primitives;

namespace Application.UnitTests.Common.Storage;

public class ImageUploadValidatorTests
{
	[Test]
	public void EnsureValid_ShouldNotThrow_WhenFileIsValid()
	{
		// Act
		Action act = () => ImageUploadValidator.EnsureValid(1024, "image/png", "Avatar");

		// Assert
		act.Should().NotThrow();
	}

	[Test]
	public void EnsureValid_ShouldThrowValidationError_WhenFileIsEmpty()
	{
		// Act
		Action act = () => ImageUploadValidator.EnsureValid(0, "image/png", "Avatar");

		// Assert
		act.Should().Throw<ResultFailureException>()
			.WithMessage("Avatar image must not be empty.")
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}

	[Test]
	public void EnsureValid_ShouldThrowValidationError_WhenFileExceedsMaxSize()
	{
		// Act
		Action act = () => ImageUploadValidator.EnsureValid(
			ImageUploadValidator.MaxFileSizeBytes + 1, "image/png", "Logo");

		// Assert
		act.Should().Throw<ResultFailureException>()
			.WithMessage("Logo image must not exceed 2 MB.")
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}

	[Test]
	public void EnsureValid_ShouldThrowValidationError_WhenContentTypeIsNotAllowed()
	{
		// Act
		Action act = () => ImageUploadValidator.EnsureValid(1024, "image/gif", "Banner");

		// Assert
		act.Should().Throw<ResultFailureException>()
			.WithMessage("Banner image must be a JPEG, PNG or WebP image.")
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}

	[Test]
	[Arguments("image/jpeg", ".jpg")]
	[Arguments("image/png", ".png")]
	[Arguments("image/webp", ".webp")]
	[Arguments("image/gif", ".jpg")]
	public void GetExtension_ShouldMapKnownContentTypes_AndFallBackToJpg(string contentType, string expectedExtension)
	{
		// Act
		var extension = ImageUploadValidator.GetExtension(contentType);

		// Assert
		extension.Should().Be(expectedExtension);
	}
}
