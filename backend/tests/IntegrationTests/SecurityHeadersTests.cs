using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class SecurityHeadersTests(IntegrationTestFixture fixture)
{
	[Test]
	public async Task GetAlive_ShouldIncludeBaselineSecurityHeaders(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/alive", cancellationToken);

		response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions).Should().BeTrue();
		contentTypeOptions.Should().ContainSingle().Which.Should().Be("nosniff");

		response.Headers.TryGetValues("X-Frame-Options", out var frameOptions).Should().BeTrue();
		frameOptions.Should().ContainSingle().Which.Should().Be("DENY");

		response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy).Should().BeTrue();
		referrerPolicy.Should().ContainSingle().Which.Should().Be("strict-origin-when-cross-origin");
	}

	// The Aspire-hosted backend under test runs with ASPNETCORE_ENVIRONMENT=Development
	// (Api's own launchSettings.json profile - AppHost.cs never overrides it for the
	// "backend" resource), which is exactly the environment HSTS is intentionally
	// skipped for (#1370): local dev serves plain HTTP, and HSTS would force a
	// browser-cached HTTPS upgrade that breaks it.
	[Test]
	public async Task GetAlive_ShouldNotIncludeHsts_WhenRunningInDevelopment(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/alive", cancellationToken);

		response.Headers.TryGetValues("Strict-Transport-Security", out _).Should().BeFalse();
	}
}
