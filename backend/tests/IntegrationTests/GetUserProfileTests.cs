using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetUserProfileTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetUserProfile_ShouldReturnProfile_WhenAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var result = await client.GetUserProfileAsync(cancellationToken);

		result.Id.Should().NotBeEmpty();
		result.Username.Should().Be("vera");
		result.Email.Should().NotBeNullOrEmpty();
	}

	[Test]
	public async Task GetUserProfile_ShouldReturnProfile_WhenAuthenticatedAsAdmin(
		CancellationToken cancellationToken)
	{
		// Regression for #760: the "admin" realm role was not composite over
		// "user"/"organisator", so an admin-only token failed the DefaultUser
		// policy that GetUserProfile (and every other baseline endpoint) requires.
		var client = await CreateAuthenticatedClientAsync("admin", "admin123");

		var result = await client.GetUserProfileAsync(cancellationToken);

		result.Id.Should().NotBeEmpty();
		result.Username.Should().Be("admin");
	}

	[Test]
	public async Task GetUserProfile_ShouldNotFail_WhenTwoConcurrentRequestsRaceTheFirstEverLoad(
		CancellationToken cancellationToken)
	{
		// Issue #1148: GetUserProfileQueryHandler lazily creates the local `user`
		// row on the very first load - a query handler has no ambient transaction
		// (TransactionPipelineBehavior only wraps commands), so two concurrent
		// first-time requests used to race a primary-key violation into an
		// unhandled 500 for whichever request lost.
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var first = client.GetUserProfileAsync(cancellationToken);
		var second = client.GetUserProfileAsync(cancellationToken);

		var results = await Task.WhenAll(first, second);

		results[0].Id.Should().Be(results[1].Id);
	}

	[Test]
	public async Task GetUserProfile_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.GetUserProfileAsync(cancellationToken);

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
