using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

// Regression coverage for #1323: AuthorizationConventionTests only proved an
// admin route carries *some* authorization decision, never that it is
// specifically EinsatzbereitAdminPolicy. These prove the four previously
// uncovered admin endpoints actually reject a non-admin (organizer) caller.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AdminEndpointAuthorizationTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ListOrganizations_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.ListOrganizationsAsync(1, 20, cancellationToken: cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task ListOrganizations_ShouldReturn403_WhenRequestingUserIsNotAdmin(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => olafClient.ListOrganizationsAsync(1, 20, cancellationToken: cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ListUsers_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.ListUsersAsync(1, 20, null, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task ListUsers_ShouldReturn403_WhenRequestingUserIsNotAdmin(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => olafClient.ListUsersAsync(1, 20, null, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task SetUserAdminStatus_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.SetUserAdminStatusAsync(
			Guid.NewGuid(), new SetUserAdminStatusRequest { IsAdmin = true }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task SetUserAdminStatus_ShouldReturn403_WhenRequestingUserIsNotAdmin(
		CancellationToken cancellationToken)
	{
		// The single highest-consequence privilege boundary in the product: a
		// plain organizer must never be able to grant themselves (or anyone
		// else) platform-admin by calling this endpoint directly.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var act = () => olafClient.SetUserAdminStatusAsync(
			vera.Id, new SetUserAdminStatusRequest { IsAdmin = true }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task SetUserEnabled_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.SetUserEnabledAsync(
			Guid.NewGuid(), new SetUserEnabledRequest { Enabled = false }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task SetUserEnabled_ShouldReturn403_WhenRequestingUserIsNotAdmin(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var act = () => olafClient.SetUserEnabledAsync(
			vera.Id, new SetUserEnabledRequest { Enabled = false }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ListAuditLogs_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.ListAuditLogsAsync(1, 20, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task ListAuditLogs_ShouldReturn403_WhenRequestingUserIsNotAdmin(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => olafClient.ListAuditLogsAsync(1, 20, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
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
