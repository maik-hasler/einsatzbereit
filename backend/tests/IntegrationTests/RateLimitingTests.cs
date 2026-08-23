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
}
