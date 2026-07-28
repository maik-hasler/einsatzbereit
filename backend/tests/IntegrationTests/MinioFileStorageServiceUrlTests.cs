using AwesomeAssertions;
using Infrastructure.Storage;

namespace IntegrationTests;

public class MinioFileStorageServiceUrlTests
{
	[Test]
	public void AppendVersionQuery_ShouldAppendUnixSecondsAsVersionQueryParam()
	{
		var uploadedOn = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

		var result = MinioFileStorageService.AppendVersionQuery(
			"https://storage.example.com/bucket/user-avatars/abc.png", uploadedOn);

		result.Should().Be($"https://storage.example.com/bucket/user-avatars/abc.png?v={uploadedOn.ToUnixTimeSeconds()}");
	}

	[Test]
	public void AppendVersionQuery_ShouldProduceDifferentVersions_WhenReuploadedAtDifferentTimes()
	{
		const string url = "https://storage.example.com/bucket/user-avatars/abc.png";
		var firstUpload = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
		var secondUpload = firstUpload.AddSeconds(1);

		var firstUrl = MinioFileStorageService.AppendVersionQuery(url, firstUpload);
		var secondUrl = MinioFileStorageService.AppendVersionQuery(url, secondUpload);

		firstUrl.Should().NotBe(secondUrl);
	}

	[Test]
	public void CacheControlHeaderValue_ShouldBePublicWithModerateMaxAge()
	{
		MinioFileStorageService.CacheControlHeaderValue.Should().Be("public, max-age=3600");
	}
}
