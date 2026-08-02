using System.Net;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class RateLimitingTests(IntegrationTestFixture fixture)
{
	// Unique IP so this test's quota is isolated from other test classes. Still
	// honored after #1332: the real backend process in this fixture is only ever
	// reached over loopback, which TrustedNetworksOptions deliberately keeps
	// trusted (see its own comment) - only a caller connecting from *outside*
	// that trusted set now has X-Forwarded-For ignored.
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
}
