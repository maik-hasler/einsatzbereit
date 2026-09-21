using AwesomeAssertions;

namespace IntegrationTests;

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
		var targetClient = await fixture.CreateAuthenticatedClientAsync(username, password);

		await targetClient.GetUserProfileAsync(cancellationToken);

		var adminClient = await fixture.CreateAuthenticatedClientAsync("admin", "admin123");
		await adminClient.AdminShadowDeleteUserAsync(userId, cancellationToken);

		var veraClient = await fixture.CreateAuthenticatedClientAsync("vera", "vera123");
		var act = () => veraClient.GetPublicUserProfileAsync(userId, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}
}
