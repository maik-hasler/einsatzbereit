using Application.Common.Exceptions;
using Domain.Primitives;

namespace Application.Common.Storage;

public static class ImageUploadValidator
{
	public const long MaxFileSizeBytes = 2 * 1024 * 1024;

	public static readonly string[] AllowedContentTypes =
		["image/jpeg", "image/png", "image/webp"];

	private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
	{
		["image/jpeg"] = ".jpg",
		["image/png"] = ".png",
		["image/webp"] = ".webp",
	};

	private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
	private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
	private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46];
	private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50];

	public static void EnsureValid(long fileLength, string contentType, string subject)
	{
		if (fileLength == 0)
			throw new ResultFailureException(Error.Validation(
				"FileUpload.Empty",
				$"{subject} image must not be empty."));

		if (fileLength > MaxFileSizeBytes)
			throw new ResultFailureException(Error.Validation(
				"FileUpload.TooLarge",
				$"{subject} image must not exceed 2 MB."));

		if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
			throw new ResultFailureException(Error.Validation(
				"FileUpload.InvalidContentType",
				$"{subject} image must be a JPEG, PNG or WebP image."));
	}

	// Returns the content-type detected from the actual bytes (magic-byte check), not the
	// client-declared header, so callers store/serve the file under its real, verified type.
	public static string EnsureValid(byte[] content, string contentType, string subject)
	{
		EnsureValid(content.Length, contentType, subject);

		return DetectContentType(content) ?? throw new ResultFailureException(Error.Validation(
			"FileUpload.InvalidContentType",
			$"{subject} image must be a JPEG, PNG or WebP image."));
	}

	public static string GetExtension(string contentType) =>
		Extensions.GetValueOrDefault(contentType, ".jpg");

	private static string? DetectContentType(byte[] content)
	{
		if (StartsWith(content, JpegSignature))
			return "image/jpeg";

		if (StartsWith(content, PngSignature))
			return "image/png";

		if (content.Length >= 12 && StartsWith(content, RiffSignature) &&
			content.AsSpan(8, 4).SequenceEqual(WebpSignature))
			return "image/webp";

		return null;
	}

	private static bool StartsWith(byte[] content, byte[] signature) =>
		content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);
}
