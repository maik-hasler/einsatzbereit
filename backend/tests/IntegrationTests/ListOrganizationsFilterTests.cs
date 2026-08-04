using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

// Coverage for #1054: the admin organization list gained Search, Deleted, and
// Flagged query params so admins can find a pending organization without
// paging through the whole table.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class ListOrganizationsFilterTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ListOrganizations_ShouldFilterByCaseInsensitiveNameSearch(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var matchName = $"Feuerwehr Alpha {Guid.NewGuid()}";
		var otherName = $"Wasserwacht Beta {Guid.NewGuid()}";
		await CreateOrganizationAsync(olafClient, matchName, cancellationToken);
		await CreateOrganizationAsync(olafClient, otherName, cancellationToken);

		var result = await adminClient.ListOrganizationsAsync(
			1, 50, search: "alpha", cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle(o => o.Name == matchName);
	}

	[Test]
	public async Task ListOrganizations_ShouldExcludeDeletedOrganizations_ByDefault(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var name = $"ToDelete {Guid.NewGuid()}";
		var orgId = await CreateOrganizationAsync(olafClient, name, cancellationToken);
		await adminClient.AdminShadowDeleteOrganizationAsync(orgId, cancellationToken);

		var result = await adminClient.ListOrganizationsAsync(
			1, 50, search: name, cancellationToken: cancellationToken);

		result.Items.Should().BeEmpty();
	}

	[Test]
	public async Task ListOrganizations_ShouldReturnOnlyDeletedOrganizations_WhenDeletedFilterIsTrue(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var deletedName = $"Deleted {Guid.NewGuid()}";
		var activeName = $"Active {Guid.NewGuid()}";
		var deletedId = await CreateOrganizationAsync(olafClient, deletedName, cancellationToken);
		await CreateOrganizationAsync(olafClient, activeName, cancellationToken);
		await adminClient.AdminShadowDeleteOrganizationAsync(deletedId, cancellationToken);

		var result = await adminClient.ListOrganizationsAsync(
			1, 50, deleted: true, cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle(o => o.Id == deletedId);
	}

	[Test]
	public async Task ListOrganizations_ShouldReturnOnlyFlaggedOrganizations_WhenFlaggedFilterIsTrue(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var flaggedName = $"Flagged {Guid.NewGuid()}";
		var unflaggedName = $"Unflagged {Guid.NewGuid()}";
		var flaggedId = await CreateOrganizationAsync(olafClient, flaggedName, cancellationToken);
		await CreateOrganizationAsync(olafClient, unflaggedName, cancellationToken);

		await veraClient.ReportOrganizationAsync(
			flaggedId,
			new ReportOrganizationRequest { Reason = "Other", Details = "Integration test report" },
			cancellationToken);

		var result = await adminClient.ListOrganizationsAsync(
			1, 50, flagged: true, cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle(o => o.Id == flaggedId);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, string name, CancellationToken cancellationToken)
	{
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = name }, cancellationToken);
		return org.Id.Value;
	}
}
