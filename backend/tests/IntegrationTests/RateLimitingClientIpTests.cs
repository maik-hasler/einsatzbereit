using Api.Common.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;

namespace IntegrationTests;

public class RateLimitingClientIpTests
{
	[Test]
	public void GetClientIp_ShouldIgnoreXForwardedForHeader_AndUseTheRealConnectionAddress()
	{
		var context = new DefaultHttpContext();
		context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
		context.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

		var clientIp = RateLimitingExtensions.GetClientIp(context);

		clientIp.Should().Be("203.0.113.7",
			"the client-supplied header must never be trusted directly - only " +
			"ForwardedHeadersMiddleware, gated on a known trusted network, may " +
			"ever change what Connection.RemoteIpAddress reports");
	}

	[Test]
	public void GetClientIp_ShouldReturnDifferentKeys_ForDifferentSpoofedHeaderValues_ProvingTheHeaderIsIgnored()
	{
		var first = new DefaultHttpContext();
		first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.1");
		first.Request.Headers["X-Forwarded-For"] = "1.1.1.1";

		var second = new DefaultHttpContext();
		second.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.1");
		second.Request.Headers["X-Forwarded-For"] = "2.2.2.2";

		RateLimitingExtensions.GetClientIp(first).Should().Be(RateLimitingExtensions.GetClientIp(second));
	}
}
