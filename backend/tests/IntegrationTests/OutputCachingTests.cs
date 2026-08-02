using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OutputCachingTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetBadgeCatalog_ShouldBeServedFromOutputCache_OnASecondRequest(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		await httpClient.GetAsync("/v1/badges", cancellationToken);
		var second = await httpClient.GetAsync("/v1/badges", cancellationToken);

		second.Headers.TryGetValues("Age", out _).Should().BeTrue(
			"the badge catalog is a public, non-personalized endpoint and should be output-cached (#1391)");
	}

	[Test]
	public async Task GetVolunteerOpportunities_ShouldBeServedFromOutputCache_OnASecondRequest(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		const string route = "/v1/volunteer-opportunities?pageNumber=1&pageSize=10";

		await httpClient.GetAsync(route, cancellationToken);
		var second = await httpClient.GetAsync(route, cancellationToken);

		second.Headers.TryGetValues("Age", out _).Should().BeTrue();
	}

	// Regression coverage for #1543: without this, a newly created/published opportunity
	// (or any other write affecting the listing) would stay invisible on the public
	// listing for up to ShortPublicReadSeconds, since nothing evicted the cached response -
	// this also caused GetVolunteerOpportunitiesTests.cs to fail non-deterministically,
	// since every test hitting this exact route/query-string shared one cached response.
	[Test]
	public async Task GetVolunteerOpportunities_ShouldReflectNewOpportunity_ImmediatelyAfterCreate(
		CancellationToken cancellationToken)
	{
		const string route = "/v1/volunteer-opportunities?pageNumber=1&pageSize=10";

		using var httpClient = fixture.CreateHttpClient();
		var before = await httpClient.GetAsync(route, cancellationToken);
		var beforeBody = await before.Content.ReadAsStringAsync(cancellationToken);
		beforeBody.Should().NotContain("Freshly published opportunity");

		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, "Cache Eviction Org", cancellationToken);

		// No IsDraft flag - published immediately, so it must appear on the very next read.
		await authenticatedClient.CreateVolunteerOpportunityAsync(new CreateVolunteerOpportunityRequest
		{
			Title = "Freshly published opportunity",
			Description = "Proves a create evicts the output cache (#1543)",
			OrganizationId = orgId,
			Occurrence = "OneTime",
			ParticipationType = "IndividualContact",
			CheckInMethod = "None",
			ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
		}, cancellationToken);

		var after = await httpClient.GetAsync(route, cancellationToken);
		var afterBody = await after.Content.ReadAsStringAsync(cancellationToken);

		afterBody.Should().Contain("Freshly published opportunity",
			"creating a volunteer opportunity must evict the listing's output cache tag " +
			"instead of leaving the previous (stale) response cached for ShortPublicReadSeconds");
	}

	// The default output cache key is method + path + query string with no per-route-parameter
	// awareness beyond that, so this also proves two different organizations don't collide on
	// (or get served) each other's cached profile response.
	[Test]
	public async Task GetPublicOrganizationProfile_ShouldCacheEachOrganizationSeparately(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var firstOrgId = await CreateOrganizationAsync(authenticatedClient, "Org One", cancellationToken);
		var secondOrgId = await CreateOrganizationAsync(authenticatedClient, "Org Two", cancellationToken);

		using var httpClient = fixture.CreateHttpClient();

		await httpClient.GetAsync($"/v1/organizations/{firstOrgId}/profile", cancellationToken);
		var firstResponse = await httpClient.GetAsync($"/v1/organizations/{firstOrgId}/profile", cancellationToken);

		await httpClient.GetAsync($"/v1/organizations/{secondOrgId}/profile", cancellationToken);
		var secondResponse = await httpClient.GetAsync($"/v1/organizations/{secondOrgId}/profile", cancellationToken);

		firstResponse.Headers.TryGetValues("Age", out _).Should().BeTrue();
		secondResponse.Headers.TryGetValues("Age", out _).Should().BeTrue();

		var firstBody = await firstResponse.Content.ReadAsStringAsync(cancellationToken);
		var secondBody = await secondResponse.Content.ReadAsStringAsync(cancellationToken);

		firstBody.Should().Contain("Org One").And.NotContain("Org Two");
		secondBody.Should().Contain("Org Two").And.NotContain("Org One");
	}

	// GetOrganizationOpportunities is authenticated and organizer-scoped - its response is not
	// the same for every caller, so it must be excluded from output caching. Caching it under
	// the default (caller-agnostic) cache key would risk serving one organizer's data to another.
	[Test]
	public async Task GetOrganizationOpportunities_ShouldNotBeOutputCached(
		CancellationToken cancellationToken)
	{
		var authenticatedClient = await CreateAuthenticatedClientAsync(cancellationToken);
		var orgId = await CreateOrganizationAsync(authenticatedClient, "Org", cancellationToken);
		var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");

		using var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var route = $"/v1/organizations/{orgId}/opportunities?status=Published&pageNumber=1&pageSize=10";

		await httpClient.GetAsync(route, cancellationToken);
		var second = await httpClient.GetAsync(route, cancellationToken);

		second.Headers.TryGetValues("Age", out _).Should().BeFalse();
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
	{
		var token = await fixture.GetAccessTokenAsync("olaf", "olaf123");
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, string name, CancellationToken cancellationToken)
	{
		var uniqueName = $"{name}_{Guid.NewGuid()}";
		var organization = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return organization.Id.Value;
	}
}
