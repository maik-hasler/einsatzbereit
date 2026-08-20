using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// Two contracts about the admin account's token and the admin-wide
/// organization listing, moved down from <c>VisualTests.JwtAudienceTests</c>
/// in einsatzbereit#2148.
///
/// The Playwright original drove a real Keycloak login, opened
/// /administration/organizations, and then - because the page lists
/// organizations alphabetically while dozens of other visual-test classes
/// create their own in the same shared session - paged through "Load more"
/// hunting for the one it had seeded. Its own comment called that "a UI
/// scavenger hunt through a live, growing, alphabetically-shifting dataset"
/// and had already been rewritten once to assert against the raw endpoint
/// instead. This is that assertion, without the browser around it.
///
/// The browser half of the audience contract is not lost: <c>AuthGuardTests</c>
/// still signs in through Keycloak's real form, and
/// <c>JwtAudienceTests.MyEngagements_AuthenticatedVera_LoadsWithoutAuthError</c>
/// still watches for a 401/403 on a page load that follows one.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AdminTokenScopeTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task AdminToken_IsAcceptedOnABaselineAuthenticatedEndpoint(
		CancellationToken cancellationToken)
	{
		// Regression for #760: the "admin" realm role had no composite roles,
		// so an admin-only token carried neither "user" nor "organisator" and
		// every endpoint behind EinsatzbereitDefaultUserPolicy returned 403 for
		// the admin account specifically. Every other test signs in as vera or
		// olaf, whose roles come from the realm JSON, so nothing caught it.
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var act = () => adminClient.GetUserProfileAsync(cancellationToken);

		await act.Should().NotThrowAsync();
	}

	[Test]
	public async Task AdminOrganizationsListing_IsNotScopedToTheCallingUser(
		CancellationToken cancellationToken)
	{
		// Regression for the PR #768 review feedback: the admin organizations
		// list used to call GET /v1/organizations, which is scoped to
		// "organizations the caller organizes" - always empty for admin, who
		// organizes nothing. Created by olaf here, who has nothing to do with
		// the admin account, so a caller-scoped endpoint could not return it.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var name = $"AdminTokenScope {Guid.NewGuid():N}";
		await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = name }, cancellationToken);

		// Searched rather than paged: the search parameter is what makes this
		// deterministic no matter how many organizations the shared session
		// has accumulated.
		var page = await adminClient.ListOrganizationsAsync(
			1, 100, name, null, null, cancellationToken);

		page.Items.Select(o => o.Name).Should().Contain(name);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
