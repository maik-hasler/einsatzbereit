using AwesomeAssertions;

namespace IntegrationTests;

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
		var adminClient = await fixture.CreateAuthenticatedClientAsync("admin", "admin123");

		var act = () => adminClient.GetUserProfileAsync(cancellationToken);

		await act.Should().NotThrowAsync();
	}

	[Test]
	public async Task AdminOrganizationsListing_IsNotScopedToTheCallingUser(
		CancellationToken cancellationToken)
	{
		var olafClient = await fixture.CreateAuthenticatedClientAsync("olaf", "olaf123");
		var adminClient = await fixture.CreateAuthenticatedClientAsync("admin", "admin123");

		var name = $"AdminTokenScope {Guid.NewGuid():N}";
		await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = name }, cancellationToken);

		var page = await adminClient.ListOrganizationsAsync(
			1, 100, name, null, null, cancellationToken);

		page.Items.Select(o => o.Name).Should().Contain(name);
	}
}
