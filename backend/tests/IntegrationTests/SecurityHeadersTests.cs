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

	[Test]
	public async Task GetAlive_ShouldNotIncludeHsts_WhenRunningInDevelopment(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/alive", cancellationToken);

		response.Headers.TryGetValues("Strict-Transport-Security", out _).Should().BeFalse();
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldNotIncludeCacheControl_WhenAnonymous(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync(
			"/v1/volunteer-opportunities?pageNumber=1&pageSize=10", cancellationToken);

		response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
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
