using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

// Regression coverage for einsatzbereit#1088 (no persisted audit trail for
// privileged actions). Exercises the full write path end to end - the API
// endpoint, the command handler's dbContext.AuditLogs.AddAsync write, and the
// ListAuditLogs read side - rather than only the write or only the read side
// in isolation, since those are covered separately by unit tests.
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AuditLogTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task ListAuditLogs_ShouldContainEntry_AfterAdminShadowDeletesOrganization(
		CancellationToken cancellationToken)
	{
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		var admin = await adminClient.GetUserProfileAsync(cancellationToken);

		var organization = await adminClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"TestOrg_{Guid.NewGuid()}" }, cancellationToken);

		await adminClient.AdminShadowDeleteOrganizationAsync(organization.Id.Value, cancellationToken);

		var result = await adminClient.ListAuditLogsAsync(1, 20, cancellationToken);

		result.Items.Should().ContainSingle(a => a.SubjectId == organization.Id.Value)
			.Which.Should().BeEquivalentTo(new
			{
				ActorUserId = admin.Id,
				ActionType = "OrganizationShadowDeleted",
				SubjectType = "Organization",
				SubjectId = organization.Id.Value,
				Reason = (string?)null,
			}, options => options.ExcludingMissingMembers());
	}

	[Test]
	public async Task ListAuditLogs_ShouldContainEntry_AfterAdminRestoresOrganization(
		CancellationToken cancellationToken)
	{
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		var admin = await adminClient.GetUserProfileAsync(cancellationToken);

		var organization = await adminClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"TestOrg_{Guid.NewGuid()}" }, cancellationToken);
		await adminClient.AdminShadowDeleteOrganizationAsync(organization.Id.Value, cancellationToken);

		await adminClient.AdminRestoreOrganizationAsync(organization.Id.Value, cancellationToken);

		var result = await adminClient.ListAuditLogsAsync(1, 20, cancellationToken);

		result.Items.Should().ContainSingle(a => a.SubjectId == organization.Id.Value && a.ActionType == "OrganizationRestored")
			.Which.ActorUserId.Should().Be(admin.Id);
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
