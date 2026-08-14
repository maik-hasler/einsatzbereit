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

		var organizationName = $"TestOrg_{Guid.NewGuid()}";
		var organization = await adminClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = organizationName }, cancellationToken);

		await adminClient.AdminShadowDeleteOrganizationAsync(organization.Id.Value, cancellationToken);

		var result = await adminClient.ListAuditLogsAsync(1, 20, cancellationToken);

		result.Items.Should().ContainSingle(a => a.SubjectId == organization.Id.Value)
			.Which.Should().BeEquivalentTo(new
			{
				ActorUserId = admin.Id,
				ActionType = "OrganizationShadowDeleted",
				SubjectType = "Organization",
				SubjectId = organization.Id.Value,
				SubjectDisplayName = organizationName,
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

	// Regression coverage for einsatzbereit#1837 - an Engagement subject has no
	// name of its own, so its audit-log entry should resolve the opportunity it
	// was a sign-up for instead of leaving the display name blank.
	[Test]
	public async Task ListAuditLogs_ShouldResolveSubjectDisplayName_ForEngagementSubject_ToItsOpportunityTitle(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var organization = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"TestOrg_{Guid.NewGuid()}" }, cancellationToken);
		var opportunityTitle = $"TestOpportunity_{Guid.NewGuid()}";
		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = opportunityTitle,
				Description = "Integration test opportunity",
				OrganizationId = organization.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.CancelEngagementAsync(
			engagement.Id,
			new CancelEngagementRequest { Reason = "Event cancelled" },
			cancellationToken);

		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");
		var result = await adminClient.ListAuditLogsAsync(1, 20, cancellationToken);

		result.Items.Should().ContainSingle(a => a.SubjectId == engagement.Id && a.ActionType == "EngagementCancelled")
			.Which.SubjectDisplayName.Should().Be(opportunityTitle);
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
