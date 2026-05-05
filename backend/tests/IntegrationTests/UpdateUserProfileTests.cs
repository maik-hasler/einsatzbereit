using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class UpdateUserProfileTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task UpdateUserProfile_ShouldPersistChanges_WhenAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		await client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { FirstName = "Vera", LastName = "Muster" },
			cancellationToken);

		var profile = await client.GetUserProfileAsync(cancellationToken);
		profile.FirstName.Should().Be("Vera");
		profile.LastName.Should().Be("Muster");
	}

	[Test]
	public async Task UpdateUserProfile_ShouldClearNames_WhenNullValuesProvided(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		await client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { FirstName = null, LastName = null },
			cancellationToken);

		var profile = await client.GetUserProfileAsync(cancellationToken);
		profile.FirstName.Should().BeNull();
		profile.LastName.Should().BeNull();
	}

	[Test]
	public async Task UpdateUserProfile_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.UpdateUserProfileAsync(
			new UpdateUserProfileRequest { FirstName = "Test" },
			cancellationToken);

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
