using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class ListUsersTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ListUsers_ShouldReturnEachUsersOwnRoles_WhenFetchedConcurrently(
		CancellationToken cancellationToken)
	{
		// Regression for #1387: KeycloakUserService.ListUsersAsync now fetches
		// per-user composite realm roles concurrently (bounded Task.WhenAll)
		// instead of one sequential HTTP call per user. The baseline seed users
		// have distinct composite role sets - vera ("user"), olaf ("user",
		// "organisator"), admin ("admin", "user", "organisator" via the
		// composite "admin" role) - so if concurrent requests ever mixed up
		// which HTTP response belonged to which user, roles would bleed
		// between them here.
		var client = await CreateAuthenticatedClientAsync("admin", "admin123");

		var result = await client.ListUsersAsync(
			pageNumber: 1, pageSize: 100, cancellationToken: cancellationToken);

		var vera = result.Items.Single(u => u.Username == "vera");
		vera.RealmRoles.Should().BeEquivalentTo(["user"]);

		var olaf = result.Items.Single(u => u.Username == "olaf");
		olaf.RealmRoles.Should().BeEquivalentTo(["user", "organisator"]);

		var admin = result.Items.Single(u => u.Username == "admin");
		admin.RealmRoles.Should().BeEquivalentTo(["admin", "user", "organisator"]);
	}

	[Test]
	public async Task ListUsers_ShouldFilterBySearchTerm(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("admin", "admin123");

		var result = await client.ListUsersAsync(
			pageNumber: 1, pageSize: 10, search: "vera", cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle().Which.Username.Should().Be("vera");
	}

	[Test]
	public async Task ListUsers_ShouldReturn403_WhenNotAdmin(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.ListUsersAsync(
			pageNumber: 1, pageSize: 10, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ListUsers_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.ListUsersAsync(
			pageNumber: 1, pageSize: 10, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
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
