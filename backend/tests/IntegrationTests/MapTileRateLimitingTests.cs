using System.Net;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class MapTileRateLimitingTests(IntegrationTestFixture fixture)
{
	private const string TestIp = "10.0.0.102";

	// zoom 0 only has a single valid tile (0/0), so x=9/y=9 is out of range -
	// GetMapTileEndpoint's IsValidTile check rejects it before ever calling
	// out to the tile provider, keeping this test fast and network-free
	// while still exercising the real rate-limiting middleware in front of it.
	private const string InvalidTileUrl = "/v1/maps/tiles/0/9/9.png";

	[Test]
	public async Task GetMapTile_ShouldNotReturn429_AtVolumeThatWouldExhaustTheAnonymousContentBucket(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", TestIp);

		var statusCodes = new List<HttpStatusCode>();

		// 65 requests would already have tripped the anonymous Read bucket's
		// default 60/min limit (see RateLimitingTests) - map tiles must have
		// their own, more generous budget so panning the map cannot starve
		// the content calls that share an IP with it (#2208).
		for (var i = 0; i < 65; i++)
		{
			var response = await httpClient.GetAsync(InvalidTileUrl, cancellationToken);
			statusCodes.Add(response.StatusCode);
		}

		statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests);
		statusCodes.Should().AllSatisfy(code => code.Should().Be(HttpStatusCode.NotFound));
	}

	// See RateLimitingTests.WaitForRateLimitWindowToResetAsync for why this
	// class needs its own reset before the next [NotInParallel("IntegrationDb")]
	// class runs.
	[After(Class)]
	public static async Task WaitForRateLimitWindowToResetAsync() =>
		await Task.Delay(TimeSpan.FromSeconds(65));
}
