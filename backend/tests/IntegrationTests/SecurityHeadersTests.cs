using System.Net.Http.Headers;
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

	// Uses /v1/organizations/directory rather than /alive: ASP.NET Core's own
	// health-check middleware sets its own Cache-Control on every /alive response
	// regardless of authentication, which would make this assertion fail for a
	// reason unrelated to what it's testing.
	[Test]
	public async Task GetPublicOrganizations_ShouldNotIncludeCacheControl_WhenAnonymous(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/v1/organizations/directory", cancellationToken);

		response.Headers.TryGetValues("Cache-Control", out _).Should().BeFalse();
	}

	[Test]
	public async Task GetUserProfile_ShouldIncludeCacheControlNoStore_WhenAuthenticated(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		var token = await fixture.GetAccessTokenAsync("vera", "vera123");
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await httpClient.GetAsync("/v1/users/me", cancellationToken);

		response.Headers.TryGetValues("Cache-Control", out var cacheControl).Should().BeTrue();
		cacheControl.Should().ContainSingle().Which.Should().Be("no-store");
	}

	// Regression for #1180: the security-header middleware used to set headers
	// directly before calling next(), with app.UseExceptionHandler() registered
	// after it. ExceptionHandlerMiddleware calls Response.Clear() when it catches
	// an exception (here, CreateEngagement's ResultFailureException for a
	// nonexistent opportunity, mapped to a 404 ProblemDetails body) - which wiped
	// any header set that way, regardless of the two middlewares' relative order.
	// The header middleware now registers via Response.OnStarting instead, which
	// runs after that Clear() and so survives it.
	[Test]
	public async Task CreateEngagement_ShouldIncludeBaselineSecurityHeaders_OnErrorResponse(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.CreateEngagementAsync(
			Guid.NewGuid(),
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);

		exception.Which.Headers.TryGetValue("X-Content-Type-Options", out var contentTypeOptions).Should().BeTrue();
		contentTypeOptions.Should().ContainSingle().Which.Should().Be("nosniff");

		exception.Which.Headers.TryGetValue("X-Frame-Options", out var frameOptions).Should().BeTrue();
		frameOptions.Should().ContainSingle().Which.Should().Be("DENY");

		// Not asserted as a single exact value: ASP.NET Core's own
		// ExceptionHandlerMiddleware also clears Cache-Control on exception
		// responses via its own OnStarting registration, so the header may carry
		// both its directives and ours - only that "no-store" is present matters.
		exception.Which.Headers.TryGetValue("Cache-Control", out var cacheControl).Should().BeTrue();
		cacheControl.Should().Contain(value => value.Contains("no-store"));
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
