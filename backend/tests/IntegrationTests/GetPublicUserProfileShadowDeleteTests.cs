using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

// Regression coverage for #1677 bug #1: GetPublicUserProfileQueryHandler used
// to only consult the filtered dbContext.Users.FindAsync for AvatarUrl/Bio/etc
// (falling back to defaults for a shadow-deleted target) while still calling
// Keycloak and still returning a fully populated PublicUserProfileResponse -
// so a shadow-deleted user's public profile stayed fully browsable instead of
// 404ing like the rest of their public presence.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetPublicUserProfileShadowDeleteTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetPublicUserProfile_ShouldReturn404_WhenTargetWasShadowDeleted(
		CancellationToken cancellationToken)
	{
		var (userId, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var targetClient = await CreateAuthenticatedClientAsync(username, password);

		// The very first profile load lazily creates the local `user` row -
		// AdminShadowDeleteUserCommandHandler looks it up via a filtered
		// Users.FindAsync and 404s if it's missing, so this must run before the
		// shadow-delete below has anything to hide.
		await targetClient.GetUserProfileAsync(cancellationToken);

		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		await adminClient.AdminShadowDeleteUserAsync(userId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var act = () => veraClient.GetPublicUserProfileAsync(userId, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
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
