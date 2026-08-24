using System.Net;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class RateLimitingTests(IntegrationTestFixture fixture)
{
	private const string TestIp = "10.0.0.99";

	[Test]
	public async Task GetVolunteerOpportunities_ShouldReturn429_WhenAnonymousRateLimitExceeded(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", TestIp);

		var statusCodes = new List<HttpStatusCode>();

		for (var i = 0; i < 65; i++)
		{
			var response = await httpClient.GetAsync(
				"/v1/volunteer-opportunities?pageNumber=1&pageSize=1", cancellationToken);
			statusCodes.Add(response.StatusCode);
		}

		statusCodes.Should().Contain(HttpStatusCode.TooManyRequests);
	}

	[Test]
	public async Task GetHealth_ShouldReturn429_WhenAnonymousRateLimitExceeded(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.100");

		var statusCodes = new List<HttpStatusCode>();

		for (var i = 0; i < 65; i++)
		{
			var response = await httpClient.GetAsync("/health", cancellationToken);
			statusCodes.Add(response.StatusCode);
		}

		statusCodes.Should().Contain(HttpStatusCode.TooManyRequests);
	}

	[Test]
	public async Task GetAlive_ShouldReturn429_WhenAnonymousRateLimitExceeded(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.101");

		var statusCodes = new List<HttpStatusCode>();

		for (var i = 0; i < 65; i++)
		{
			var response = await httpClient.GetAsync("/alive", cancellationToken);
			statusCodes.Add(response.StatusCode);
		}

		statusCodes.Should().Contain(HttpStatusCode.TooManyRequests);
	}

	// The anonymous rate limiter is keyed by the real connection IP, not X-Forwarded-For
	// (see GetClientIp), so the bursts above exhaust a bucket shared with every other
	// anonymous-endpoint test in the [NotInParallel("IntegrationDb")] queue. Wait out the
	// fixed window (60s, see RateLimitingOptions.ReadOptions.WindowSeconds) before the next
	// serialized class runs, so it doesn't inherit this class's exhausted quota.
	[After(Class)]
	public static async Task WaitForRateLimitWindowToResetAsync() =>
		await Task.Delay(TimeSpan.FromSeconds(65));
}
