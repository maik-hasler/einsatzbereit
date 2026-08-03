using Api.Common.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;

namespace IntegrationTests;

// Regression coverage for #1332: GetClientIp used to read X-Forwarded-For directly,
// trusting it unconditionally with no verification that the immediate caller was
// actually the deployment's own reverse proxy - any client could set a different
// value per request and get a fresh anonymous rate-limit bucket every time.
//
// The real defense now lives in ForwardedHeadersMiddleware (Program.cs +
// TrustedNetworksOptions): it only rewrites HttpContext.Connection.RemoteIpAddress
// from X-Forwarded-For when the immediate connection came from a known trusted
// network, and leaves it alone otherwise. This test doesn't need to exercise that
// middleware itself (that's Microsoft's own well-tested code) - it proves the one
// thing our code is responsible for: GetClientIp must key off Connection.RemoteIpAddress
// alone and never read the header directly, no matter what it contains.
public class RateLimitingClientIpTests
{
	[Test]
	public void GetClientIp_ShouldIgnoreXForwardedForHeader_AndUseTheRealConnectionAddress()
	{
		// Arrange: a caller pretending to be someone else on every request - exactly
		// the bypass this issue describes - alongside the real (unspoofable)
		// connection address a socket-level peer actually has.
		var context = new DefaultHttpContext();
		context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
		context.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

		// Act
		var clientIp = RateLimitingExtensions.GetClientIp(context);

		// Assert
		clientIp.Should().Be("203.0.113.7",
			"the client-supplied header must never be trusted directly - only " +
			"ForwardedHeadersMiddleware, gated on a known trusted network, may " +
			"ever change what Connection.RemoteIpAddress reports");
	}

	[Test]
	public void GetClientIp_ShouldReturnDifferentKeys_ForDifferentSpoofedHeaderValues_ProvingTheHeaderIsIgnored()
	{
		// A caller rotating X-Forwarded-For per request used to get a fresh rate
		// limit partition every time - if the header were still honored here, these
		// two contexts (same real connection, different spoofed headers) would
		// produce two different keys instead of the same one.
		var first = new DefaultHttpContext();
		first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.1");
		first.Request.Headers["X-Forwarded-For"] = "1.1.1.1";

		var second = new DefaultHttpContext();
		second.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.1");
		second.Request.Headers["X-Forwarded-For"] = "2.2.2.2";

		RateLimitingExtensions.GetClientIp(first).Should().Be(RateLimitingExtensions.GetClientIp(second));
	}
}
