using AwesomeAssertions;
using Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class StorageHealthCheckTests(IntegrationTestFixture fixture)
{
	[Test]
	public async Task PingAsync_ShouldSucceed_WhenMinioIsReachable(CancellationToken cancellationToken)
	{
		var storage = new MinioFileStorageService(Options.Create(new StorageSettings
		{
			Endpoint = fixture.GetMinioEndpoint(),
			AccessKey = "minio",
			SecretKey = "minio123",
			BucketName = "einsatzbereit",
		}));

		var act = () => storage.PingAsync(cancellationToken);

		await act.Should().NotThrowAsync();
	}

	// Regression guard for #1081: before this, nothing in the backend's own
	// readiness probe checked storage connectivity at all, so /health reported
	// Healthy even while MinIO was completely unreachable and every upload/image
	// fetch was failing.
	[Test]
	public async Task PingAsync_ShouldThrow_WhenMinioIsUnreachable(CancellationToken cancellationToken)
	{
		var storage = new MinioFileStorageService(Options.Create(new StorageSettings
		{
			// Nothing listens on this port - connection refused immediately, no
			// real network round trip to wait out.
			Endpoint = "http://127.0.0.1:1",
			AccessKey = "minio",
			SecretKey = "minio123",
			BucketName = "einsatzbereit",
		}));

		var act = () => storage.PingAsync(cancellationToken);

		await act.Should().ThrowAsync<Exception>();
	}

	[Test]
	public async Task GetHealth_ShouldReturnHealthy_WhenStorageIsReachable(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/health", cancellationToken);

		response.IsSuccessStatusCode.Should().BeTrue();
	}
}
