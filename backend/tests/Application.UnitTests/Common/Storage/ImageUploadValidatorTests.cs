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

	private static byte[] JpegBytes => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

	private static byte[] PngBytes => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

	private static byte[] WebpBytes =>
		[0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

	[Test]
	public void EnsureValid_WithContentBytes_ShouldReturnDetectedType_ForJpegMagicBytes()
	{
		// Act
		var contentType = ImageUploadValidator.EnsureValid(JpegBytes, "image/jpeg", "Avatar");

		// Assert
		contentType.Should().Be("image/jpeg");
	}

	[Test]
	public void EnsureValid_WithContentBytes_ShouldReturnDetectedType_ForPngMagicBytes()
	{
		// Act
		var contentType = ImageUploadValidator.EnsureValid(PngBytes, "image/png", "Logo");

		// Assert
		contentType.Should().Be("image/png");
	}

	[Test]
	public void EnsureValid_WithContentBytes_ShouldReturnDetectedType_ForWebpMagicBytes()
	{
		// Act
		var contentType = ImageUploadValidator.EnsureValid(WebpBytes, "image/webp", "Banner");

		// Assert
		contentType.Should().Be("image/webp");
	}

	[Test]
	public void EnsureValid_WithContentBytes_ShouldThrow_WhenDeclaredTypeIsSpoofed()
	{
		// Arrange
		var htmlBytes = "<script>alert(1)</script>"u8.ToArray();

		// Act
		Action act = () => ImageUploadValidator.EnsureValid(htmlBytes, "image/png", "Avatar");

		// Assert
		act.Should().Throw<ResultFailureException>()
			.WithMessage("Avatar image must be a JPEG, PNG or WebP image.")
			.Which.Error.Type.Should().Be(ErrorType.Validation);
	}

	[Test]
	public void EnsureValid_WithContentBytes_ShouldReturnRealType_WhenDeclaredTypeDoesNotMatchActualBytes()
	{
		// Arrange

		// Act
		var contentType = ImageUploadValidator.EnsureValid(JpegBytes, "image/png", "Avatar");

		// Assert
		contentType.Should().Be("image/jpeg");
	}
}
