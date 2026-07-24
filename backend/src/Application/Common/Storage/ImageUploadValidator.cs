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

	public static string GetExtension(string contentType) =>
		Extensions.GetValueOrDefault(contentType, ".jpg");
}
